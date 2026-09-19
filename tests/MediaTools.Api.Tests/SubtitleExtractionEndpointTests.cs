using System.Net;
using System.Net.Http.Json;
using System.Text;
using MediaTools.Api.Models;
using MediaTools.Api.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace MediaTools.Api.Tests;

public sealed class SubtitleExtractionEndpointTests
{
    [Fact]
    public async Task Extract_MissingPath_ReturnsBadRequest()
    {
        using var factory = new ExtractionApplicationFactory();
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync(
            "/api/media/extract-subtitle",
            new ExtractSubtitleRequest(null, 3, "pt-BR"));

        await AssertErrorAsync(response, HttpStatusCode.BadRequest, "invalid_path");
    }

    [Theory]
    [InlineData("{")]
    [InlineData("")]
    public async Task Extract_InvalidJson_ReturnsInvalidRequest(string payload)
    {
        using var factory = new ExtractionApplicationFactory();
        using var client = factory.CreateClient();
        using var content = new StringContent(payload, Encoding.UTF8, "application/json");

        using var response = await client.PostAsync("/api/media/extract-subtitle", content);

        await AssertErrorAsync(response, HttpStatusCode.BadRequest, "invalid_request");
    }

    [Fact]
    public async Task Extract_WrongContentType_ReturnsUnsupportedMediaType()
    {
        using var factory = new ExtractionApplicationFactory();
        using var client = factory.CreateClient();
        using var content = new StringContent("{}", Encoding.UTF8, "text/plain");

        using var response = await client.PostAsync("/api/media/extract-subtitle", content);

        await AssertErrorAsync(response, HttpStatusCode.UnsupportedMediaType, "unsupported_media_type");
    }

    [Fact]
    public async Task Extract_OversizedBody_ReturnsRequestTooLarge()
    {
        using var factory = new ExtractionApplicationFactory();
        using var client = factory.CreateClient();
        using var content = new StringContent(
            "{\"path\":\"/media/" + new string('x', 17_000) +
            "\",\"streamIndex\":3,\"targetLanguage\":\"pt-BR\"}",
            Encoding.UTF8,
            "application/json");

        using var response = await client.PostAsync("/api/media/extract-subtitle", content);

        await AssertErrorAsync(response, HttpStatusCode.RequestEntityTooLarge, "request_too_large");
    }

    [Fact]
    public async Task Extract_ConcurrentRequestsToSameDestination_ReturnOneCreatedAndOneConflict()
    {
        using var factory = new ExtractionApplicationFactory(
            extractHandler: (_, _, temporaryPath, cancellationToken) =>
                File.WriteAllTextAsync(temporaryPath, "subtitle", cancellationToken));
        var sourcePath = factory.CreateMediaFile("video.mkv");
        using var client = factory.CreateClient();
        var request = new ExtractSubtitleRequest(sourcePath, 3, "pt-BR");

        var first = client.PostAsJsonAsync("/api/media/extract-subtitle", request);
        var second = client.PostAsJsonAsync("/api/media/extract-subtitle", request);
        var responses = await Task.WhenAll(first, second);

        Assert.Equal(
            [HttpStatusCode.Created, HttpStatusCode.Conflict],
            responses.Select(response => response.StatusCode).OrderBy(status => status).ToArray());
        var outputPath = Path.Combine(factory.MediaRoot, "video.pt-BR.srt");
        Assert.Equal("subtitle", await File.ReadAllTextAsync(outputPath));
        Assert.Empty(Directory.GetFiles(factory.MediaRoot, "*.tmp"));

        foreach (var response in responses)
        {
            response.Dispose();
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData(-1)]
    public async Task Extract_InvalidStreamIndex_ReturnsBadRequest(int? streamIndex)
    {
        using var factory = new ExtractionApplicationFactory();
        var sourcePath = factory.CreateMediaFile("video.mkv");
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync(
            "/api/media/extract-subtitle",
            new ExtractSubtitleRequest(sourcePath, streamIndex, "pt-BR"));

        await AssertErrorAsync(
            response,
            HttpStatusCode.BadRequest,
            "invalid_stream_index");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("../pt-BR")]
    [InlineData("pt/BR")]
    [InlineData("pt BR")]
    public async Task Extract_InvalidTargetLanguage_ReturnsBadRequest(string? targetLanguage)
    {
        using var factory = new ExtractionApplicationFactory();
        var sourcePath = factory.CreateMediaFile("video.mkv");
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync(
            "/api/media/extract-subtitle",
            new ExtractSubtitleRequest(sourcePath, 3, targetLanguage));

        await AssertErrorAsync(
            response,
            HttpStatusCode.BadRequest,
            "invalid_target_language");
    }

    [Fact]
    public async Task Extract_MissingMediaFile_ReturnsNotFound()
    {
        using var factory = new ExtractionApplicationFactory();
        var sourcePath = Path.Combine(factory.MediaRoot, "missing.mkv");
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync(
            "/api/media/extract-subtitle",
            new ExtractSubtitleRequest(sourcePath, 3, "pt-BR"));

        await AssertErrorAsync(response, HttpStatusCode.NotFound, "file_not_found");
    }

    [Fact]
    public async Task Extract_MissingSubtitleStream_ReturnsNotFound()
    {
        using var factory = new ExtractionApplicationFactory(
            probeHandler: (_, _) =>
                Task.FromResult<IReadOnlyCollection<SubtitleStream>>(
                    Array.Empty<SubtitleStream>()));
        var sourcePath = factory.CreateMediaFile("video.mkv");
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync(
            "/api/media/extract-subtitle",
            new ExtractSubtitleRequest(sourcePath, 3, "pt-BR"));

        await AssertErrorAsync(
            response,
            HttpStatusCode.NotFound,
            "subtitle_stream_not_found");
    }

    [Fact]
    public async Task Extract_UnsupportedCodec_ReturnsUnprocessableEntity()
    {
        using var factory = new ExtractionApplicationFactory(
            probeHandler: (_, _) =>
                Task.FromResult<IReadOnlyCollection<SubtitleStream>>(
                    [new SubtitleStream(3, "hdmv_pgs_subtitle", "por", null)]));
        var sourcePath = factory.CreateMediaFile("video.mkv");
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync(
            "/api/media/extract-subtitle",
            new ExtractSubtitleRequest(sourcePath, 3, "pt-BR"));

        await AssertErrorAsync(
            response,
            HttpStatusCode.UnprocessableEntity,
            "unsupported_subtitle_codec");
    }

    [Fact]
    public async Task Extract_ExistingDestination_ReturnsConflictWithoutOverwriting()
    {
        using var factory = new ExtractionApplicationFactory();
        var sourcePath = factory.CreateMediaFile("video.mkv");
        var outputPath = Path.Combine(factory.MediaRoot, "video.pt-BR.srt");
        await File.WriteAllTextAsync(outputPath, "existing subtitle");
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync(
            "/api/media/extract-subtitle",
            new ExtractSubtitleRequest(sourcePath, 3, "pt-BR"));

        await AssertErrorAsync(
            response,
            HttpStatusCode.Conflict,
            "subtitle_already_exists");
        Assert.Equal("existing subtitle", await File.ReadAllTextAsync(outputPath));
    }

    [Fact]
    public async Task Extract_ValidRequest_ReturnsCreatedAndMovesNonEmptyFile()
    {
        using var factory = new ExtractionApplicationFactory();
        var sourcePath = factory.CreateMediaFile(
            "[Erai-raws] Planetes - 01 [720p].mkv");
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync(
            "/api/media/extract-subtitle",
            new ExtractSubtitleRequest(sourcePath, 3, "pt-BR"));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<ExtractSubtitleResponse>();
        Assert.NotNull(result);
        Assert.Equal(sourcePath, result.SourcePath);
        Assert.Equal(3, result.StreamIndex);
        Assert.Equal("ass", result.Codec);
        Assert.Equal("por", result.Language);
        Assert.Equal("pt-BR", result.TargetLanguage);
        Assert.Equal(
            Path.Combine(
                factory.MediaRoot,
                "[Erai-raws] Planetes - 01 [720p].pt-BR.srt"),
            result.OutputPath);
        Assert.True(result.WrittenBytes > 0);
        Assert.True(File.Exists(result.OutputPath));
        Assert.Empty(Directory.GetFiles(factory.MediaRoot, "*.tmp"));
    }

    [Theory]
    [InlineData("failed", HttpStatusCode.UnprocessableEntity, "ffmpeg_failed")]
    [InlineData("unavailable", HttpStatusCode.ServiceUnavailable, "ffmpeg_unavailable")]
    [InlineData("timeout", HttpStatusCode.GatewayTimeout, "extract_timeout")]
    public async Task Extract_ControlledFfmpegFailure_ReturnsExpectedErrorAndCleansTemporary(
        string failure,
        HttpStatusCode expectedStatus,
        string expectedError)
    {
        using var factory = new ExtractionApplicationFactory(
            extractHandler: async (_, _, temporaryPath, cancellationToken) =>
            {
                await File.WriteAllTextAsync(
                    temporaryPath,
                    "temporary",
                    cancellationToken);
                throw CreateFfmpegException(failure);
            });
        var sourcePath = factory.CreateMediaFile("video.mkv");
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync(
            "/api/media/extract-subtitle",
            new ExtractSubtitleRequest(sourcePath, 3, "pt-BR"));

        await AssertErrorAsync(response, expectedStatus, expectedError);
        Assert.Empty(Directory.GetFiles(factory.MediaRoot, "*.tmp"));
    }

    [Theory]
    [InlineData("failed", HttpStatusCode.UnprocessableEntity, "ffprobe_failed")]
    [InlineData("unavailable", HttpStatusCode.ServiceUnavailable, "ffprobe_unavailable")]
    [InlineData("timeout", HttpStatusCode.GatewayTimeout, "probe_timeout")]
    public async Task Extract_ControlledProbeFailure_ReturnsExpectedError(
        string failure,
        HttpStatusCode expectedStatus,
        string expectedError)
    {
        using var factory = new ExtractionApplicationFactory(
            probeHandler: (_, _) =>
                Task.FromException<IReadOnlyCollection<SubtitleStream>>(
                    CreateFfprobeException(failure)));
        var sourcePath = factory.CreateMediaFile("video.mkv");
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync(
            "/api/media/extract-subtitle",
            new ExtractSubtitleRequest(sourcePath, 3, "pt-BR"));

        await AssertErrorAsync(response, expectedStatus, expectedError);
    }

    [Fact]
    public async Task Extract_EmptyTemporaryFile_ReturnsFfmpegFailedAndCleansTemporary()
    {
        using var factory = new ExtractionApplicationFactory(
            extractHandler: (_, _, temporaryPath, _) =>
            {
                File.WriteAllText(temporaryPath, string.Empty);
                return Task.CompletedTask;
            });
        var sourcePath = factory.CreateMediaFile("video.mkv");
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync(
            "/api/media/extract-subtitle",
            new ExtractSubtitleRequest(sourcePath, 3, "pt-BR"));

        await AssertErrorAsync(
            response,
            HttpStatusCode.UnprocessableEntity,
            "ffmpeg_failed");
        Assert.Empty(Directory.GetFiles(factory.MediaRoot, "*.tmp"));
    }

    [Fact]
    public async Task Extract_MissingTemporaryFile_ReturnsFfmpegFailed()
    {
        using var factory = new ExtractionApplicationFactory(
            extractHandler: (_, _, _, _) => Task.CompletedTask);
        var sourcePath = factory.CreateMediaFile("video.mkv");
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync(
            "/api/media/extract-subtitle",
            new ExtractSubtitleRequest(sourcePath, 3, "pt-BR"));

        await AssertErrorAsync(
            response,
            HttpStatusCode.UnprocessableEntity,
            "ffmpeg_failed");
    }

    [Fact]
    public async Task Extract_DestinationCreatedDuringExtraction_ReturnsConflictAndPreservesIt()
    {
        string? finalOutputPath = null;
        using var factory = new ExtractionApplicationFactory(
            extractHandler: async (_, _, temporaryPath, cancellationToken) =>
            {
                await File.WriteAllTextAsync(temporaryPath, "new subtitle", cancellationToken);
                await File.WriteAllTextAsync(
                    finalOutputPath!,
                    "concurrent subtitle",
                    cancellationToken);
            });
        var sourcePath = factory.CreateMediaFile("video.mkv");
        finalOutputPath = Path.Combine(factory.MediaRoot, "video.pt-BR.srt");
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync(
            "/api/media/extract-subtitle",
            new ExtractSubtitleRequest(sourcePath, 3, "pt-BR"));

        await AssertErrorAsync(
            response,
            HttpStatusCode.Conflict,
            "subtitle_already_exists");
        Assert.Equal("concurrent subtitle", await File.ReadAllTextAsync(finalOutputPath));
        Assert.Empty(Directory.GetFiles(factory.MediaRoot, "*.tmp"));
    }

    private static Exception CreateFfmpegException(string failure) => failure switch
    {
        "failed" => new FfmpegFailedException("Test failure."),
        "unavailable" => new FfmpegUnavailableException(
            "Test failure.",
            new InvalidOperationException()),
        "timeout" => new FfmpegTimeoutException("Test failure."),
        _ => throw new ArgumentOutOfRangeException(nameof(failure))
    };

    private static Exception CreateFfprobeException(string failure) => failure switch
    {
        "failed" => new FfprobeFailedException("Test failure."),
        "unavailable" => new FfprobeUnavailableException(
            "Test failure.",
            new InvalidOperationException()),
        "timeout" => new FfprobeTimeoutException("Test failure."),
        _ => throw new ArgumentOutOfRangeException(nameof(failure))
    };

    private static async Task AssertErrorAsync(
        HttpResponseMessage response,
        HttpStatusCode expectedStatus,
        string expectedError)
    {
        Assert.Equal(expectedStatus, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.NotNull(error);
        Assert.Equal(expectedError, error.Error);
        Assert.False(string.IsNullOrWhiteSpace(error.Message));
    }

    private sealed class ExtractionApplicationFactory : WebApplicationFactory<Program>
    {
        private readonly IFfprobeService _probeService;
        private readonly IFfmpegSubtitleExtractor _extractor;

        public ExtractionApplicationFactory(
            Func<string, CancellationToken,
                Task<IReadOnlyCollection<SubtitleStream>>>? probeHandler = null,
            Func<string, int, string, CancellationToken, Task>? extractHandler = null)
        {
            MediaRoot = Path.Combine(
                Path.GetTempPath(),
                $"mediatools-extraction-tests-{Guid.NewGuid():N}");
            Directory.CreateDirectory(MediaRoot);
            _probeService = new FakeProbeService(probeHandler);
            _extractor = new FakeSubtitleExtractor(extractHandler);
        }

        public string MediaRoot { get; }

        public string CreateMediaFile(string relativePath)
        {
            var fullPath = Path.Combine(MediaRoot, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
            File.WriteAllText(fullPath, string.Empty);
            return fullPath;
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureAppConfiguration((_, configuration) =>
            {
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["MediaTools:MediaRoot"] = MediaRoot,
                    ["MediaTools:FfprobePath"] = "ffprobe",
                    ["MediaTools:FfmpegPath"] = "ffmpeg",
                    ["MediaTools:ProbeTimeoutSeconds"] = "30",
                    ["MediaTools:ExtractTimeoutSeconds"] = "120"
                });
            });

            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IFfprobeService>();
                services.RemoveAll<IFfmpegSubtitleExtractor>();
                services.AddSingleton(_probeService);
                services.AddSingleton(_extractor);
            });
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            if (disposing && Directory.Exists(MediaRoot))
            {
                Directory.Delete(MediaRoot, recursive: true);
            }
        }
    }

    private sealed class FakeProbeService : IFfprobeService
    {
        private readonly Func<string, CancellationToken,
            Task<IReadOnlyCollection<SubtitleStream>>> _handler;

        public FakeProbeService(
            Func<string, CancellationToken,
                Task<IReadOnlyCollection<SubtitleStream>>>? handler)
        {
            _handler = handler ?? ((_, _) =>
                Task.FromResult<IReadOnlyCollection<SubtitleStream>>(
                    [new SubtitleStream(3, "ass", "por", "Portuguese")]));
        }

        public Task<IReadOnlyCollection<SubtitleStream>> ProbeAsync(
            string path,
            CancellationToken cancellationToken) =>
            _handler(path, cancellationToken);
    }

    private sealed class FakeSubtitleExtractor : IFfmpegSubtitleExtractor
    {
        private readonly Func<string, int, string, CancellationToken, Task> _handler;

        public FakeSubtitleExtractor(
            Func<string, int, string, CancellationToken, Task>? handler)
        {
            _handler = handler ?? (async (_, _, temporaryPath, cancellationToken) =>
                await File.WriteAllTextAsync(
                    temporaryPath,
                    "1\n00:00:00,000 --> 00:00:01,000\nSubtitle\n",
                    cancellationToken));
        }

        public Task ExtractAsync(
            string sourcePath,
            int streamIndex,
            string temporaryOutputPath,
            CancellationToken cancellationToken) =>
            _handler(sourcePath, streamIndex, temporaryOutputPath, cancellationToken);
    }
}
