using MediaTools.Api.Errors;

namespace MediaTools.Api.Services;

public sealed class FfprobeFailedException : MediaToolsException
{
    public FfprobeFailedException(
        string? technicalDetail = null,
        Exception? innerException = null)
        : base(
            "ffprobe_failed",
            StatusCodes.Status422UnprocessableEntity,
            "Unable to inspect media file.",
            innerException)
    {
    }
}

public sealed class FfprobeUnavailableException : MediaToolsException
{
    public FfprobeUnavailableException(
        string? technicalDetail,
        Exception innerException)
        : base(
            "ffprobe_unavailable",
            StatusCodes.Status503ServiceUnavailable,
            "The media inspection service is unavailable.",
            innerException)
    {
    }
}

public sealed class FfprobeTimeoutException : MediaToolsException
{
    public FfprobeTimeoutException(string? technicalDetail = null)
        : base(
            "probe_timeout",
            StatusCodes.Status504GatewayTimeout,
            "The media inspection timed out.")
    {
    }
}
