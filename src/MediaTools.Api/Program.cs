using MediaTools.Api.Errors;
using MediaTools.Api.Endpoints;
using MediaTools.Api.Options;
using MediaTools.Api.Services;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services
    .AddOptions<MediaToolsOptions>()
    .Bind(builder.Configuration.GetSection(MediaToolsOptions.SectionName))
    .ValidateOnStart();
builder.Services.AddSingleton<IValidateOptions<MediaToolsOptions>, MediaToolsOptionsValidator>();
builder.Services.AddExceptionHandler<MediaToolsExceptionHandler>();

builder.Services.AddSingleton<IMediaPathValidator, MediaPathValidator>();
builder.Services.AddSingleton<IFfprobeService, FfprobeService>();
builder.Services.AddSingleton<IFfmpegSubtitleExtractor, FfmpegSubtitleExtractor>();
builder.Services.AddSingleton<ISubtitleExtractionService, SubtitleExtractionService>();
builder.Services.AddHostedService<MediaRootStartupDiagnostics>();

var app = builder.Build();
app.UseMiddleware<RequestSizeLimitMiddleware>();
app.UseExceptionHandler(new ExceptionHandlerOptions
{
    ExceptionHandler = async context =>
    {
        var exception = context.Features.Get<IExceptionHandlerFeature>()?.Error;
        if (exception is not null)
        {
            await context.RequestServices
                .GetRequiredService<IExceptionHandler>()
                .TryHandleAsync(context, exception, context.RequestAborted);
        }
    }
});
app.UseStatusCodePages(ErrorStatusCodePages.HandleAsync);

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapGet("/health", () =>
    Results.Ok(new
    {
        status = "healthy"
    }));

app.MapMediaProbeEndpoints();
app.MapSubtitleExtractionEndpoints();

app.Run();

public partial class Program;
