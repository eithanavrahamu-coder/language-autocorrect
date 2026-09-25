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

    public static VoiceInformation? HebrewVoice =>
        SafeVoices().FirstOrDefault(v => v.Language.StartsWith("he", StringComparison.OrdinalIgnoreCase));

    public static VoiceInformation? EnglishVoice =>
        SafeVoices().FirstOrDefault(v => v.Language.StartsWith("en", StringComparison.OrdinalIgnoreCase));

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
            string text;
            var he = HebrewVoice;
            var en = EnglishVoice;
            if (lang == Lang.Hebrew && he != null)
            {
                synth.Voice = he;
                text = "עברית";
            }
            else
            {
                if (en != null) synth.Voice = en;
                text = lang == Lang.Hebrew ? "Hebrew" : "English";
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
