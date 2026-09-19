using MediaTools.Api.Models;
using MediaTools.Api.Options;
using MediaTools.Api.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace MediaTools.Api.Tests;

public sealed class SubtitleExtractionServiceTests : IDisposable
{
    private readonly string _mediaRoot;

    public SubtitleExtractionServiceTests()
    {
        _mediaRoot = Path.Combine(
            Path.GetTempPath(),
            $"mediatools-service-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_mediaRoot);
    }

    [Fact]
    public async Task ExtractAsync_CancellationDuringExtraction_CleansTemporaryFile()
    {
        var sourcePath = Path.Combine(_mediaRoot, "video.mkv");
        await File.WriteAllTextAsync(sourcePath, string.Empty);
        var pathValidator = new MediaPathValidator(
            Microsoft.Extensions.Options.Options.Create(new MediaToolsOptions
            {
                MediaRoot = _mediaRoot
            }));
        var service = new SubtitleExtractionService(
            new StubProbeService(),
            new CancelingSubtitleExtractor(),
            pathValidator,
            NullLogger<SubtitleExtractionService>.Instance);

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            service.ExtractAsync(sourcePath, 3, "pt-BR", CancellationToken.None));

        Assert.Empty(Directory.GetFiles(_mediaRoot, "*.tmp"));
        Assert.False(File.Exists(Path.Combine(_mediaRoot, "video.pt-BR.srt")));
    }

    public void Dispose()
    {
        if (Directory.Exists(_mediaRoot))
        {
            Directory.Delete(_mediaRoot, recursive: true);
        }
    }

    private sealed class StubProbeService : IFfprobeService
    {
        public Task<IReadOnlyCollection<SubtitleStream>> ProbeAsync(
            string path,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyCollection<SubtitleStream>>(
                [new SubtitleStream(3, "ass", "por", "Portuguese")]);
    }

    private sealed class CancelingSubtitleExtractor : IFfmpegSubtitleExtractor
    {
        public async Task ExtractAsync(
            string sourcePath,
            int streamIndex,
            string temporaryOutputPath,
            CancellationToken cancellationToken)
        {
            await File.WriteAllTextAsync(
                temporaryOutputPath,
                "temporary subtitle",
                cancellationToken);
            throw new OperationCanceledException();
        }
    }
}
