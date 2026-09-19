using Microsoft.AspNetCore.Diagnostics;

namespace MediaTools.Api.Errors;

public sealed class MediaToolsExceptionHandler : IExceptionHandler
{
    private readonly ILogger<MediaToolsExceptionHandler> _logger;

    public MediaToolsExceptionHandler(ILogger<MediaToolsExceptionHandler> logger)
    {
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (exception is OperationCanceledException &&
            httpContext.RequestAborted.IsCancellationRequested)
        {
            _logger.LogInformation(
                "Request cancellation received for {RequestPath}.",
                httpContext.Request.Path);
            return true;
        }

        var errorCode = "internal_error";
        var statusCode = StatusCodes.Status500InternalServerError;

        if (exception is MediaToolsException mediaToolsException)
        {
            errorCode = mediaToolsException.ErrorCode;
            statusCode = mediaToolsException.StatusCode;
            _logger.LogWarning(
                "Known MediaTools error {ErrorCode} while processing {RequestPath}.",
                errorCode,
                httpContext.Request.Path);
        }
        else if (exception is BadHttpRequestException badRequestException)
        {
            (errorCode, statusCode) = badRequestException.StatusCode switch
            {
                StatusCodes.Status413PayloadTooLarge =>
                    ("request_too_large", StatusCodes.Status413PayloadTooLarge),
                StatusCodes.Status415UnsupportedMediaType =>
                    ("unsupported_media_type", StatusCodes.Status415UnsupportedMediaType),
                _ => ("invalid_request", StatusCodes.Status400BadRequest)
            };

            _logger.LogInformation(
                "Invalid HTTP request for {RequestPath}: {ErrorCode}.",
                httpContext.Request.Path,
                errorCode);
        }
        else
        {
            _logger.LogError(
                exception,
                "Unhandled exception while processing {RequestPath}.",
                httpContext.Request.Path);
        }

        if (httpContext.Response.HasStarted)
        {
            return false;
        }

        httpContext.Response.StatusCode = statusCode;
        await httpContext.Response.WriteAsJsonAsync(
            ErrorResponseCatalog.Create(errorCode),
            cancellationToken);
        return true;
    }
}
