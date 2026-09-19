using MediaTools.Api.Models;

namespace MediaTools.Api.Services;

public interface ISubtitleExtractionService
{
    Task<ExtractSubtitleResponse> ExtractAsync(
        string sourcePath,
        int streamIndex,
        string targetLanguage,
        CancellationToken cancellationToken);
}
