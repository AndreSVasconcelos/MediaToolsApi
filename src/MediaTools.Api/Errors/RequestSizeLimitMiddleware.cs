namespace MediaTools.Api.Errors;

public sealed class RequestSizeLimitMiddleware
{
    private const long MaxBodyBytes = 16 * 1024;
    private readonly RequestDelegate _next;

    public RequestSizeLimitMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.Request.Method == HttpMethods.Post &&
            (context.Request.Path == "/api/media/probe" ||
             context.Request.Path == "/api/media/extract-subtitle") &&
            context.Request.ContentLength > MaxBodyBytes)
        {
            context.Response.StatusCode = StatusCodes.Status413PayloadTooLarge;
            context.Response.ContentType = "application/json; charset=utf-8";
            await context.Response.WriteAsJsonAsync(
                ErrorResponseCatalog.Create("request_too_large"));
            return;
        }

        await _next(context);
    }
}
