using Microsoft.Extensions.Options;

namespace MediaTools.Api.Options;

public sealed class MediaRootStartupDiagnostics : IHostedService
{
    private readonly MediaToolsOptions _options;
    private readonly ILogger<MediaRootStartupDiagnostics> _logger;

    public MediaRootStartupDiagnostics(
        IOptions<MediaToolsOptions> options,
        ILogger<MediaRootStartupDiagnostics> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (!Directory.Exists(_options.MediaRoot))
        {
            _logger.LogWarning(
                "Configured MediaRoot {MediaRoot} does not exist. " +
                "Media operations will return file-not-found until it is available.",
                _options.MediaRoot);
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
