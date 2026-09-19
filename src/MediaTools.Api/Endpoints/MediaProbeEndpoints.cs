using MediaTools.Api.Errors;
using MediaTools.Api.Models;
using MediaTools.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace MediaTools.Api.Endpoints;

public static class MediaProbeEndpoints
{
    public const long MaxJsonRequestBytes = 16 * 1024;

    public static IEndpointRouteBuilder MapMediaProbeEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/media/probe", ProbeMediaAsync)
            .WithName("ProbeMedia")
            .Accepts<ProbeRequest>("application/json")
            .Produces<ProbeResponse>(StatusCodes.Status200OK)
            .Produces<ErrorResponse>(StatusCodes.Status400BadRequest)
            .Produces<ErrorResponse>(StatusCodes.Status404NotFound)
            .Produces<ErrorResponse>(StatusCodes.Status409Conflict)
            .Produces<ErrorResponse>(StatusCodes.Status413PayloadTooLarge)
            .Produces<ErrorResponse>(StatusCodes.Status415UnsupportedMediaType)
            .Produces<ErrorResponse>(StatusCodes.Status422UnprocessableEntity)
            .Produces<ErrorResponse>(StatusCodes.Status503ServiceUnavailable)
            .Produces<ErrorResponse>(StatusCodes.Status504GatewayTimeout)
            .Produces<ErrorResponse>(StatusCodes.Status500InternalServerError)
            .WithMetadata(new RequestSizeLimitAttribute(MaxJsonRequestBytes));

        return endpoints;
    }

    private static async Task<IResult> ProbeMediaAsync(
        ProbeRequest request,
        IMediaPathValidator pathValidator,
        IFfprobeService ffprobeService,
        CancellationToken cancellationToken)
    {
        var validation = pathValidator.Validate(request.Path);

        if (validation.Error == MediaPathValidationError.InvalidPath)
        {
            return ErrorResults.Create("invalid_path");
        }

        if (validation.Error == MediaPathValidationError.FileNotFound)
        {
            return ErrorResults.Create("file_not_found");
        }

        var fullPath = validation.FullPath!;

        var subtitles = await ffprobeService.ProbeAsync(fullPath, cancellationToken);
        return Results.Ok(new ProbeResponse(fullPath, subtitles));
    }
}
