using System;
using System.IO;
using System.Linq;
using System.Media;
using System.Threading;
using System.Threading.Tasks;
using LayoutBuddy.Engine;
using Windows.Media.SpeechSynthesis;

namespace LayoutBuddy;

/// <summary>Speaks the layout name. Rapid changes are debounced so only the last one is spoken.</summary>
internal sealed class VoiceAnnouncer : IDisposable
{
    private readonly Timer _debounce;
    private Lang _pending;
    private SoundPlayer? _player;
    private MemoryStream? _audio;
    private readonly object _lock = new();

    public VoiceAnnouncer()
    {
        _debounce = new Timer(_ => _ = SpeakAsync(_pending), null, Timeout.Infinite, Timeout.Infinite);
    }

    public static VoiceInformation? VoiceFor(Lang lang) =>
        SafeVoices().FirstOrDefault(v => v.Language.StartsWith(Languages.Get(lang).Code, StringComparison.OrdinalIgnoreCase));

    public static VoiceInformation? HebrewVoice => VoiceFor(Lang.Hebrew);
    public static VoiceInformation? EnglishVoice => VoiceFor(Lang.English);

    private static VoiceInformation[] SafeVoices()
    {
        try { return SpeechSynthesizer.AllVoices.ToArray(); }
        catch { return []; }
    }

    public void Announce(Lang lang)
    {
        _pending = lang;
        _debounce.Change(350, Timeout.Infinite);
    }

    public async Task SpeakAsync(Lang lang)
    {
        try
        {
            using var synth = new SpeechSynthesizer();
            // The language's own name in its own voice ("Русский"), or its English name if no such voice is installed.
            var info = Languages.Get(lang);
            string text;
            if (VoiceFor(lang) is { } native)
            {
                synth.Voice = native;
                text = info.NativeName;
            }
            else
            {
                if (EnglishVoice is { } en) synth.Voice = en;
                text = info.Name;
            }

            using var stream = await synth.SynthesizeTextToStreamAsync(text);
            var ms = new MemoryStream();
            await stream.AsStreamForRead().CopyToAsync(ms);
            ms.Position = 0;

            lock (_lock)
            {
                _player?.Stop();
                _player?.Dispose();
                _audio?.Dispose();
                _audio = ms;
                _player = new SoundPlayer(ms);
                _player.Play();
            }
        }
        catch (Exception ex)
        {
            Log.Write("Voice error: " + ex.Message);
        }
    }

    public void Dispose()
    {
        _debounce.Dispose();
        lock (_lock)
        {
            _player?.Dispose();
            _audio?.Dispose();
        }
    }
}
