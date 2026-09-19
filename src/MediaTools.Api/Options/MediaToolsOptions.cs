namespace MediaTools.Api.Options;

public sealed class MediaToolsOptions
{
    public const string SectionName = "MediaTools";

    public string MediaRoot { get; init; } = "/media";

    public string FfprobePath { get; init; } = "ffprobe";

    public string FfmpegPath { get; init; } = "ffmpeg";

    public int ProbeTimeoutSeconds { get; init; } = 30;

    public int ExtractTimeoutSeconds { get; init; } = 120;
}
