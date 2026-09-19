namespace MediaTools.Api.Services;

public interface IFfmpegSubtitleExtractor
{
    Task ExtractAsync(
        string sourcePath,
        int streamIndex,
        string temporaryOutputPath,
        CancellationToken cancellationToken);
}
