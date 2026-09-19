using System.Text.RegularExpressions;

namespace MediaTools.Api.Services;

internal static partial class SubtitleExtractionPolicy
{
    private static readonly HashSet<string> SupportedCodecs = new(
        ["subrip", "ass", "ssa", "webvtt", "mov_text"],
        StringComparer.OrdinalIgnoreCase);

    public static bool IsValidTargetLanguage(string? targetLanguage) =>
        targetLanguage is not null && TargetLanguagePattern().IsMatch(targetLanguage);

    public static bool IsSupportedCodec(string? codec) =>
        codec is not null && SupportedCodecs.Contains(codec);

    public static string BuildOutputPath(string sourcePath, string targetLanguage)
    {
        var directory = Path.GetDirectoryName(sourcePath)
            ?? throw new ArgumentException("Source path must contain a directory.", nameof(sourcePath));
        var baseName = Path.GetFileNameWithoutExtension(sourcePath);

        return Path.Combine(directory, $"{baseName}.{targetLanguage}.srt");
    }

    [GeneratedRegex("^[A-Za-z]{2,3}(?:-[A-Za-z]{2,4})?$")]
    private static partial Regex TargetLanguagePattern();
}
