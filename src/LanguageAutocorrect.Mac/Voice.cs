using System;
using System.Linq;
using AVFoundation;
using Foundation;
using LanguageAutocorrect.Engine;

namespace LanguageAutocorrect.Mac;

/// <summary>Says the keyboard's language with the Mac's own voices. Quick changes in a row say only the last one.</summary>
internal sealed class Voice
{
    private readonly AVSpeechSynthesizer _synth = new();
    private NSTimer? _debounce;

    public static AVSpeechSynthesisVoice? VoiceFor(Lang lang)
    {
        var code = Languages.Get(lang).Code;
        try
        {
            return AVSpeechSynthesisVoice.GetSpeechVoices().FirstOrDefault(v =>
                v.Language.Equals(code, StringComparison.OrdinalIgnoreCase) ||
                v.Language.StartsWith(code + "-", StringComparison.OrdinalIgnoreCase));
        }
        catch (Exception)
        {
            return null;
        }
    }

    public void Announce(Lang lang)
    {
        _debounce?.Invalidate();
        _debounce = NSTimer.CreateScheduledTimer(0.35, false, _ => Speak(lang));
    }

    public void Speak(Lang lang)
    {
        try
        {
            // The language's own name in its own voice ("Русский"), or its English name if there's no such voice.
            var info = Languages.Get(lang);
            var native = VoiceFor(lang);
            var utterance = new AVSpeechUtterance(native != null ? info.NativeName : info.Name)
            {
                Voice = native ?? VoiceFor(Lang.English),
            };
            _synth.StopSpeaking(AVSpeechBoundary.Immediate);
            _synth.SpeakUtterance(utterance);
        }
        catch (Exception ex)
        {
            Log.Write("Voice error: " + ex.Message);
        }
    }
}
