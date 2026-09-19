using MediaTools.Api.Errors;
using MediaTools.Api.Models;
using MediaTools.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace MediaTools.Api.Endpoints;

public static class SubtitleExtractionEndpoints
{
    public const long MaxJsonRequestBytes = 16 * 1024;

    public static IEndpointRouteBuilder MapSubtitleExtractionEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/media/extract-subtitle", ExtractSubtitleAsync)
            .WithName("ExtractSubtitle")
            .Accepts<ExtractSubtitleRequest>("application/json")
            .Produces<ExtractSubtitleResponse>(StatusCodes.Status201Created)
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

    private static async Task<IResult> ExtractSubtitleAsync(
        ExtractSubtitleRequest request,
        IMediaPathValidator pathValidator,
        ISubtitleExtractionService extractionService,
        CancellationToken cancellationToken)
    {
        if (request.StreamIndex is null or < 0)
        {
            return ErrorResults.Create("invalid_stream_index");
        }

        if (!SubtitleExtractionPolicy.IsValidTargetLanguage(request.TargetLanguage))
        {
            return ErrorResults.Create("invalid_target_language");
        }

        var pathValidation = pathValidator.Validate(request.Path);

        if (pathValidation.Error == MediaPathValidationError.InvalidPath)
        {
            return ErrorResults.Create("invalid_path");
        }

        if (pathValidation.Error == MediaPathValidationError.FileNotFound)
        {
            return ErrorResults.Create("file_not_found");
        }

        var response = await extractionService.ExtractAsync(
            pathValidation.FullPath!,
            request.StreamIndex.Value,
            request.TargetLanguage!,
            cancellationToken);

        return Results.Json(response, statusCode: StatusCodes.Status201Created);
    }
}
