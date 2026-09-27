using LanguageAutocorrect.Engine;

namespace LanguageAutocorrect.Engine.Tests;

public static class Shared
{
    private static readonly Lazy<WrongLayoutDetector> _detector = new(WrongLayoutDetector.LoadDefault);
    public static WrongLayoutDetector Detector => _detector.Value;

    public static readonly Lazy<LanguageModel> English = new(() => LanguageModel.Load(Lang.English));
    public static readonly Lazy<LanguageModel> Hebrew = new(() => LanguageModel.Load(Lang.Hebrew));

    /// <summary>US keys for a word typed in English (identity) or Hebrew.</summary>
    public static string Keys(string word) => KeyMap.ToUsKeys(word);
}
