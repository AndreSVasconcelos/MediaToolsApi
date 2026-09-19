using Microsoft.AspNetCore.Diagnostics;

namespace MediaTools.Api.Errors;

public static class ErrorStatusCodePages
{
    public static async Task HandleAsync(StatusCodeContext context)
    {
        var response = context.HttpContext.Response;

        if (response.HasStarted ||
            response.ContentLength is not null ||
            context.HttpContext.Request.Path == "/health")
        {
            return;
        }

        var errorCode = response.StatusCode switch
        {
            StatusCodes.Status413PayloadTooLarge => "request_too_large",
            StatusCodes.Status415UnsupportedMediaType => "unsupported_media_type",
            StatusCodes.Status400BadRequest => "invalid_request",
            StatusCodes.Status500InternalServerError => "internal_error",
            _ => null
        };

        if (errorCode is null)
        {
            return;
        }

        response.ContentType = "application/json; charset=utf-8";
        await response.WriteAsJsonAsync(ErrorResponseCatalog.Create(errorCode));
    }
}
