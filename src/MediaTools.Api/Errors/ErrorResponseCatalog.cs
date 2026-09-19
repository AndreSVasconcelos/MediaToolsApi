using MediaTools.Api.Models;

namespace MediaTools.Api.Errors;

public static class ErrorResponseCatalog
{
    public static ErrorResponse Create(string errorCode) => errorCode switch
    {
        "invalid_path" => new(errorCode, "The provided media path is invalid."),
        "file_not_found" => new(errorCode, "The media file was not found."),
        "ffprobe_failed" => new(errorCode, "Unable to inspect media file."),
        "ffprobe_unavailable" => new(errorCode, "The media inspection service is unavailable."),
        "probe_timeout" => new(errorCode, "The media inspection timed out."),
        "invalid_stream_index" => new(
            errorCode,
            "Stream index must be present and greater than or equal to zero."),
        "invalid_target_language" => new(errorCode, "Target language is invalid."),
        "subtitle_stream_not_found" => new(
            errorCode,
            "The requested subtitle stream was not found."),
        "subtitle_already_exists" => new(
            errorCode,
            "Target subtitle file already exists."),
        "unsupported_subtitle_codec" => new(
            errorCode,
            "The requested subtitle codec is not supported."),
        "ffmpeg_failed" => new(errorCode, "Unable to extract subtitle stream."),
        "ffmpeg_unavailable" => new(
            errorCode,
            "The subtitle extraction service is unavailable."),
        "extract_timeout" => new(errorCode, "Subtitle extraction timed out."),
        "invalid_request" => new(errorCode, "The request body is invalid."),
        "request_too_large" => new(errorCode, "The request body is too large."),
        "unsupported_media_type" => new(
            errorCode,
            "The request Content-Type must be application/json."),
        "internal_error" => new(errorCode, "An unexpected error occurred."),
        _ => new(errorCode, "The request could not be processed.")
    };

    public static IResult Result(string errorCode) =>
        Results.Json(Create(errorCode), statusCode: StatusCode(errorCode));

    public static int StatusCode(string errorCode) => errorCode switch
    {
        "invalid_path" or "invalid_stream_index" or "invalid_target_language" or
            "invalid_request" => StatusCodes.Status400BadRequest,
        "file_not_found" or "subtitle_stream_not_found" => StatusCodes.Status404NotFound,
        "subtitle_already_exists" => StatusCodes.Status409Conflict,
        "request_too_large" => StatusCodes.Status413PayloadTooLarge,
        "unsupported_media_type" => StatusCodes.Status415UnsupportedMediaType,
        "ffprobe_failed" or "ffmpeg_failed" or "unsupported_subtitle_codec" =>
            StatusCodes.Status422UnprocessableEntity,
        "ffprobe_unavailable" or "ffmpeg_unavailable" =>
            StatusCodes.Status503ServiceUnavailable,
        "probe_timeout" or "extract_timeout" => StatusCodes.Status504GatewayTimeout,
        "internal_error" => StatusCodes.Status500InternalServerError,
        _ => StatusCodes.Status500InternalServerError
    };
}
