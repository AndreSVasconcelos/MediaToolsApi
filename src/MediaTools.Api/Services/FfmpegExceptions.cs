using MediaTools.Api.Errors;

namespace MediaTools.Api.Services;

public sealed class FfmpegFailedException : MediaToolsException
{
    public FfmpegFailedException(string? technicalDetail = null)
        : base(
            "ffmpeg_failed",
            StatusCodes.Status422UnprocessableEntity,
            "Unable to extract subtitle stream.")
    {
    }
}

public sealed class FfmpegUnavailableException : MediaToolsException
{
    public FfmpegUnavailableException(
        string? technicalDetail,
        Exception innerException)
        : base(
            "ffmpeg_unavailable",
            StatusCodes.Status503ServiceUnavailable,
            "The subtitle extraction service is unavailable.",
            innerException)
    {
    }
}

public sealed class FfmpegTimeoutException : MediaToolsException
{
    public FfmpegTimeoutException(string? technicalDetail = null)
        : base(
            "extract_timeout",
            StatusCodes.Status504GatewayTimeout,
            "Subtitle extraction timed out.")
    {
    }
}
