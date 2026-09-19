namespace MediaTools.Api.Models;

public sealed record ProbeResponse(
    string Path,
    IReadOnlyCollection<SubtitleStream> Subtitles);
