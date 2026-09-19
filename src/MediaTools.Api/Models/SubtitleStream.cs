namespace MediaTools.Api.Models;

public sealed record SubtitleStream(
    int Index,
    string? Codec,
    string? Language,
    string? Title);
