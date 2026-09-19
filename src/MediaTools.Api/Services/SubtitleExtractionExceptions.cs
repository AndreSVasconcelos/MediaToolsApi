using MediaTools.Api.Errors;

namespace MediaTools.Api.Services;

public sealed class SubtitleStreamNotFoundException : MediaToolsException
{
    public SubtitleStreamNotFoundException()
        : base(
            "subtitle_stream_not_found",
            StatusCodes.Status404NotFound,
            "The requested subtitle stream was not found.")
    {
    }
}

public sealed class UnsupportedSubtitleCodecException : MediaToolsException
{
    public UnsupportedSubtitleCodecException()
        : base(
            "unsupported_subtitle_codec",
            StatusCodes.Status422UnprocessableEntity,
            "The requested subtitle codec is not supported.")
    {
    }
}

public sealed class SubtitleAlreadyExistsException : MediaToolsException
{
    public SubtitleAlreadyExistsException()
        : base(
            "subtitle_already_exists",
            StatusCodes.Status409Conflict,
            "Target subtitle file already exists.")
    {
    }
}

public sealed class InvalidSubtitleOutputPathException : MediaToolsException
{
    public InvalidSubtitleOutputPathException()
        : base(
            "invalid_path",
            StatusCodes.Status400BadRequest,
            "The provided media path is invalid.")
    {
    }
}
