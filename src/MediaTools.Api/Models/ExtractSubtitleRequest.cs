namespace MediaTools.Api.Models;

public sealed record ExtractSubtitleRequest(
    string? Path,
    int? StreamIndex,
    string? TargetLanguage);
