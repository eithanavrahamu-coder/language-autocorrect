using System;
using System.Collections.Generic;
using System.Linq;
using CoreFoundation;
using LanguageAutocorrect.Engine;
using ObjCRuntime;

namespace LanguageAutocorrect.Mac;

/// <summary>
/// The keyboards (input sources) added in System Settings → Keyboard → Text Input: which language each one types, what
/// each key types on it, which one is on, and switching between them. Main thread only: macOS requires it.
/// </summary>
internal static unsafe class InputSources
{
    /// <summary>A keyboard the user added, read once.</summary>
    /// <param name="Lang">The supported language it types, or null.</param>
    private sealed record Keyboard(string Id, Lang? Lang, string[]? Tokens, string[]? Shifted,
        IReadOnlyDictionary<(char Accent, char Letter), char>? Joins);

    private static readonly IntPtr Lib = Dlfcn.dlopen(Native.HIToolbox, 0);
    private static readonly IntPtr PropCategory = Dlfcn.GetIntPtr(Lib, "kTISPropertyInputSourceCategory");
    private static readonly IntPtr PropSelectable = Dlfcn.GetIntPtr(Lib, "kTISPropertyInputSourceIsSelectCapable");
    private static readonly IntPtr PropId = Dlfcn.GetIntPtr(Lib, "kTISPropertyInputSourceID");
    private static readonly IntPtr PropLanguages = Dlfcn.GetIntPtr(Lib, "kTISPropertyInputSourceLanguages");
    private static readonly IntPtr PropLayout = Dlfcn.GetIntPtr(Lib, "kTISPropertyUnicodeKeyLayoutData");
    private static readonly string? KeyboardCategory = Str(Dlfcn.GetIntPtr(Lib, "kTISCategoryKeyboardInputSource"));

    /// <summary>
    /// Languages the Mac app leaves out for now. Korean is typed with an input method that builds each syllable before
    /// it's finished, so a fix can't delete exactly what's on the screen.
    /// </summary>
    public static bool Unsupported(Lang lang) => lang == Lang.Korean;

    private static readonly Dictionary<string, Keyboard> ById = new();
    private static List<Keyboard> _enabled = new();
    /// <summary>The keyboard last used for each language (when a language has several, like ABC and U.S.).</summary>
    private static readonly Dictionary<Lang, string> LastUsed = new();
    /// <summary>The keyboard whose keys the engine uses for each language.</summary>
    private static readonly Dictionary<Lang, string> Loaded = new();

    // ---------------- physical keys ----------------

    // macOS key codes of the 47 word keys, in KeyMap.PhysicalKeys order: ` 1 2 … = q w … \ a s … ' z x … /
    private static readonly ushort[] Keycodes =
    [
        0x32, 0x12, 0x13, 0x14, 0x15, 0x17, 0x16, 0x1A, 0x1C, 0x19, 0x1D, 0x1B, 0x18,
        0x0C, 0x0D, 0x0E, 0x0F, 0x11, 0x10, 0x20, 0x22, 0x1F, 0x23, 0x21, 0x1E, 0x2A,
        0x00, 0x01, 0x02, 0x03, 0x05, 0x04, 0x26, 0x28, 0x25, 0x29, 0x27,
        0x06, 0x07, 0x08, 0x09, 0x0B, 0x2D, 0x2E, 0x2B, 0x2F, 0x2C,
    ];

    public const ushort SpaceKey = 0x31, ReturnKey = 0x24, EnterKey = 0x4C, BackspaceKey = 0x33, ZKey = 0x06;
    private const ushort GraveKey = 0x32, SectionKey = 0x0A;

    /// <summary>
    /// The key left of 1. Apple's ISO keyboards (most of Europe) send another key code for it, and use the grave key's
    /// code for the extra key beside the left Shift.
    /// </summary>
    private static ushort _topLeftKey = GraveKey;

    private static readonly Dictionary<int, char> KeyByCode = BuildKeys();

    private static Dictionary<int, char> BuildKeys()
    {
        var map = new Dictionary<int, char>();
        for (int i = 0; i < Keycodes.Length; i++) map[Keycodes[i]] = KeyMap.PhysicalKeys[i];
        return map;
    }

    /// <summary>The key's character on a US keyboard, if it's one of the keys that can be part of a word.</summary>
    public static bool PhysicalKey(int keycode, out char key)
    {
        if (keycode is GraveKey or SectionKey)
        {
            key = '`';
            return keycode == _topLeftKey;
        }
        return KeyByCode.TryGetValue(keycode, out key);
    }

    // ---------------- which keyboard is on ----------------

    /// <summary>The language typed right now, or null for a keyboard of another language (or an input method).</summary>
    public static Lang? Current()
    {
        var id = CurrentId();
        if (id == null) return null;
        if (ById.TryGetValue(id, out var k)) return k.Lang;
        if (Unknown.Contains(id)) return null;
        // Added since the last look, most likely.
        Refresh();
        if (ById.TryGetValue(id, out k)) return k.Lang;
        Unknown.Add(id);
        return null;
    }

    /// <summary>Input sources that aren't in the list (so the list isn't read again on every key).</summary>
    private static readonly HashSet<string> Unknown = new();

    public static string? CurrentId()
    {
        var source = Native.TISCopyCurrentKeyboardInputSource();
        if (source == IntPtr.Zero) return null;
        try { return Str(Native.TISGetInputSourceProperty(source, PropId)); }
        finally { Native.CFRelease(source); }
    }

    /// <summary>
    /// Notes the keyboard that's on (called often): remembers it as its language's keyboard, and teaches the engine its
    /// keys if the language has several keyboards and this one wasn't the one in use.
    /// </summary>
    public static Lang? Observe()
    {
        var id = CurrentId();
        if (id == null) return null;
        var lang = Current();
        if (lang is not Lang l) return null;
        LastUsed[l] = id;
        if (!Loaded.TryGetValue(l, out var loaded) || loaded != id) Load(ById[id]);
        return l;
    }

    // ---------------- the keyboards added ----------------

    /// <summary>The supported languages that have a keyboard added in System Settings, in its order.</summary>
    public static IReadOnlyList<Lang> InstalledLanguages() =>
        Languages.TypedBy(_enabled.Select(k => k.Lang).OfType<Lang>());

    /// <summary>
    /// Reads the keyboards added in System Settings again and teaches the engine what each key types on them, so
    /// layouts like French – PC, Swiss German or Hebrew – QWERTY are handled exactly. Returns true if the list changed.
    /// </summary>
    public static bool Refresh()
    {
        var topLeft = Native.KBGetLayoutType(Native.LMGetKbdType()) == Native.IsoKeyboard ? SectionKey : GraveKey;
        if (topLeft != _topLeftKey)
        {
            // Another kind of keyboard was plugged in: read the keyboards again.
            _topLeftKey = topLeft;
            ById.Clear();
            Loaded.Clear();
        }
        Unknown.Clear();
        var list = Native.TISCreateInputSourceList(IntPtr.Zero, false);
        if (list == IntPtr.Zero) return false;
        var found = new List<Keyboard>();
        try
        {
            for (nint i = 0, n = Native.CFArrayGetCount(list); i < n; i++)
            {
                var source = Native.CFArrayGetValueAtIndex(list, i);
                if (Str(Native.TISGetInputSourceProperty(source, PropCategory)) != KeyboardCategory) continue;
                if (!IsTrue(Native.TISGetInputSourceProperty(source, PropSelectable))) continue;
                if (Str(Native.TISGetInputSourceProperty(source, PropId)) is not { } id) continue;
                if (!ById.TryGetValue(id, out var k))
                {
                    try { k = Read(source, id); }
                    catch (Exception ex)
                    {
                        Log.Write($"Reading keyboard {id} failed: {ex.Message}");
                        k = new Keyboard(id, null, null, null, null);
                    }
                    ById[id] = k;
                }
                found.Add(k);
            }
        }
        finally
        {
            Native.CFRelease(list);
        }

        bool changed = !found.Select(k => k.Id).SequenceEqual(_enabled.Select(k => k.Id));
        _enabled = found;
        foreach (var lang in found.Select(k => k.Lang).OfType<Lang>().Distinct())
        {
            var preferred = LastUsed.TryGetValue(lang, out var last) && found.Exists(k => k.Id == last)
                ? last
                : found.First(k => k.Lang == lang).Id;
            if (!Loaded.TryGetValue(lang, out var loaded) || loaded != preferred) Load(ById[preferred]);
        }
        return changed;
    }

    private static void Load(Keyboard k)
    {
        if (k.Lang is not Lang lang || k.Tokens == null) return;
        try
        {
            KeyMap.SetKeyboard(lang, k.Tokens, k.Shifted, k.Joins);
            Loaded[lang] = k.Id;
        }
        catch (Exception ex)
        {
            Log.Write($"Using keyboard {k.Id} failed: {ex.Message}");
        }
    }

    /// <summary>Switches to <paramref name="target"/>'s keyboard (the one used last for it), unless it's on already.</summary>
    public static void Switch(Lang target)
    {
        if (Current() == target) return;
        var id = LastUsed.TryGetValue(target, out var last) && _enabled.Exists(k => k.Id == last)
            ? last
            : _enabled.FirstOrDefault(k => k.Lang == target)?.Id;
        if (id == null) return;
        var list = Native.TISCreateInputSourceList(IntPtr.Zero, false);
        if (list == IntPtr.Zero) return;
        try
        {
            for (nint i = 0, n = Native.CFArrayGetCount(list); i < n; i++)
            {
                var source = Native.CFArrayGetValueAtIndex(list, i);
                if (Str(Native.TISGetInputSourceProperty(source, PropId)) != id) continue;
                int status = Native.TISSelectInputSource(source);
                if (status != 0) Log.Write($"Switching to {id} failed: {status}");
                return;
            }
        }
        finally
        {
            Native.CFRelease(list);
        }
    }

    /// <summary>
    /// For --selfcheck: reads keyboards that come with every Mac (added or not) and checks a word typed on each of them,
    /// so the build can see that reading keyboards works.
    /// </summary>
    public static IEnumerable<(bool Ok, string Text)> SelfCheck()
    {
        var expected = new (string Id, Lang Lang, string Keys, string Word)[]
        {
            ("com.apple.keylayout.Hebrew", Lang.Hebrew, "akuo", "שלום"),
            ("com.apple.keylayout.Russian", Lang.Russian, "ghbdtn", "привет"),
            ("com.apple.keylayout.Greek", Lang.Greek, "kalhmera", "καλημερα"),
            ("com.apple.keylayout.German", Lang.German, "yeit", "zeit"),
            ("com.apple.keylayout.French", Lang.French, ";qison", "maison"),
        };
        var found = new Dictionary<string, Keyboard>();
        var list = Native.TISCreateInputSourceList(IntPtr.Zero, true);
        if (list != IntPtr.Zero)
        {
            try
            {
                for (nint i = 0, n = Native.CFArrayGetCount(list); i < n; i++)
                {
                    var source = Native.CFArrayGetValueAtIndex(list, i);
                    if (Str(Native.TISGetInputSourceProperty(source, PropId)) is { } id && expected.Any(e => e.Id == id))
                        found[id] = Read(source, id);
                }
            }
            finally
            {
                Native.CFRelease(list);
            }
        }
        foreach (var e in expected)
        {
            if (!found.TryGetValue(e.Id, out var k))
            {
                yield return (false, $"{e.Id}: not found");
                continue;
            }
            if (k.Lang != e.Lang || k.Tokens == null)
            {
                yield return (false, $"{e.Id}: read as {k.Lang?.ToString() ?? "no supported language"}");
                continue;
            }
            Load(k);
            var typed = KeyMap.Render(e.Keys, e.Lang);
            yield return (typed == e.Word, $"{Languages.Get(e.Lang).Name} keyboard ({e.Id}): {e.Keys} → {typed}" +
                (k.Joins is { Count: > 0 } joins ? $", {joins.Count} accent joins" : ""));
        }
    }

    // ---------------- reading a keyboard ----------------

    private static Keyboard Read(IntPtr source, string id)
    {
        var data = Native.TISGetInputSourceProperty(source, PropLayout);
        // Input methods (Japanese, Chinese, Korean) have no key layout of their own.
        if (data == IntPtr.Zero) return new Keyboard(id, null, null, null, null);
        var layout = Native.CFDataGetBytePtr(data);
        var tokens = ReadKeys(layout, shift: false);
        var lang = LanguageOf(source, tokens);
        if (lang is not Lang l || tokens == null) return new Keyboard(id, null, null, null, null);
        // Georgian and similar keyboards type letters of their own with Shift.
        var shifted = Languages.Get(l).ShiftKeyboard != null ? ReadKeys(layout, shift: true) : null;
        return new Keyboard(id, l, tokens, shifted, ReadJoins(layout));
    }

    /// <summary>
    /// The language a keyboard is listed under, unless it clearly types another script (a Latin keyboard listed
    /// under Serbian).
    /// </summary>
    private static Lang? LanguageOf(IntPtr source, string[]? tokens)
    {
        var languages = Native.TISGetInputSourceProperty(source, PropLanguages);
        if (languages == IntPtr.Zero || Native.CFArrayGetCount(languages) == 0) return null;
        var code = Str(Native.CFArrayGetValueAtIndex(languages, 0))?.Split('-', '_')[0];
        var info = Languages.FromCode(code);
        if (info == null || Unsupported(info.Lang) || tokens == null) return null;
        return tokens.Count(t => info.IsLetter(t[0])) < 10 ? null : info.Lang;
    }

    private const uint ShiftModifier = 0x02; // (shiftKey >> 8) & 0xFF

    /// <summary>What one key types, by itself (accent keys wait for the next key: <paramref name="dead"/> is set).</summary>
    private static string Translate(IntPtr layout, ushort keycode, bool shift, ref uint dead)
    {
        char* buf = stackalloc char[8];
        Native.UCKeyTranslate(layout, keycode, 0 /* key down */, shift ? ShiftModifier : 0, Native.LMGetKbdType(), 0,
            ref dead, 8, out var length, buf);
        return new string(buf, 0, (int)length);
    }

    /// <summary>The accent an accent key shows when it's followed by Space.</summary>
    private static char Accent(IntPtr layout, uint dead)
    {
        var text = Translate(layout, SpaceKey, false, ref dead);
        return text.Length > 0 ? text[0] : '´';
    }

    /// <summary>
    /// What each word key types, as tokens for <see cref="KeyMap.SetKeyboard"/>, or null if this doesn't look like a
    /// keyboard that types letters.
    /// </summary>
    private static string[]? ReadKeys(IntPtr layout, bool shift)
    {
        var tokens = new string[Keycodes.Length];
        int letters = 0;
        for (int i = 0; i < Keycodes.Length; i++)
        {
            uint dead = 0;
            ushort code = i == 0 ? _topLeftKey : Keycodes[i];
            var text = Translate(layout, code, shift, ref dead);
            if (text.Length == 0 && dead != 0)
            {
                // An accent key: a combining accent, and the accent shown by itself.
                char spacing = Accent(layout, dead);
                tokens[i] = CombiningFor(spacing) is char c ? $"~{c}{spacing}" : spacing.ToString();
            }
            else if (text.Length > 0 && !text.Any(char.IsControl) && !text.Contains(' '))
            {
                tokens[i] = text;
                if (char.IsLetter(text[0])) letters++;
            }
            else
            {
                tokens[i] = "�"; // this key types nothing here
            }
        }
        return shift || letters >= 20 ? tokens : null;
    }

    /// <summary>
    /// Which letters each accent key joins with on this keyboard (´ then e is é, ´ then m is "´m"). A fix deletes as many
    /// characters as were typed, so this has to be exact.
    /// </summary>
    private static IReadOnlyDictionary<(char Accent, char Letter), char>? ReadJoins(IntPtr layout)
    {
        var accents = new List<(ushort Code, bool Shift, uint Dead, char Accent)>();
        var letters = new List<(ushort Code, bool Shift, char Letter)>();
        for (int i = 0; i < Keycodes.Length; i++)
        {
            ushort code = i == 0 ? _topLeftKey : Keycodes[i];
            foreach (bool shift in new[] { false, true })
            {
                uint dead = 0;
                var text = Translate(layout, code, shift, ref dead);
                if (text.Length == 0 && dead != 0)
                {
                    char accent = Accent(layout, dead);
                    if (!accents.Exists(a => a.Accent == accent)) accents.Add((code, shift, dead, accent));
                }
                else if (text.Length == 1)
                {
                    letters.Add((code, shift, text[0]));
                }
            }
        }
        if (accents.Count == 0) return null;

        var joins = new Dictionary<(char Accent, char Letter), char>();
        foreach (var a in accents)
        {
            foreach (var l in letters)
            {
                uint dead = a.Dead;
                var text = Translate(layout, l.Code, l.Shift, ref dead);
                if (text.Length == 1) joins[(a.Accent, l.Letter)] = text[0];
            }
        }
        return joins;
    }

    private static char? CombiningFor(char spacing) => spacing switch
    {
        '´' or '΄' or '\'' => '́',
        '`' => '̀',
        '^' or 'ˆ' => '̂',
        '¨' => '̈',
        '~' or '˜' => '̃',
        'ˇ' => '̌',
        '¸' => '̧',
        _ => null,
    };

    // ---------------- Core Foundation values ----------------

    private static string? Str(IntPtr cfString) => cfString == IntPtr.Zero ? null : CFString.FromHandle(cfString);

    private static bool IsTrue(IntPtr cfBoolean) =>
        cfBoolean != IntPtr.Zero && Native.CFGetTypeID(cfBoolean) == Native.CFBooleanGetTypeID() && Native.CFBooleanGetValue(cfBoolean);
}
