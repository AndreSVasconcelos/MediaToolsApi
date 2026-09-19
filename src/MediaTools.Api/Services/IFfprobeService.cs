using MediaTools.Api.Models;

namespace MediaTools.Api.Services;

public interface IFfprobeService
{
    Task<IReadOnlyCollection<SubtitleStream>> ProbeAsync(
        string path,
        CancellationToken cancellationToken);
}
