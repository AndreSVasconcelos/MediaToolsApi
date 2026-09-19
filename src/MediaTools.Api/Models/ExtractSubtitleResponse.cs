namespace MediaTools.Api.Models;

public sealed record ExtractSubtitleResponse(
    string SourcePath,
    int StreamIndex,
    string? Codec,
    string? Language,
    string TargetLanguage,
    string OutputPath,
    long WrittenBytes);
