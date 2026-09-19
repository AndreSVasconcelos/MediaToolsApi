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

public sealed class MediaProbeEndpointTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("relative/video.mkv")]
    [InlineData("/etc/passwd")]
    public async Task Probe_InvalidPath_ReturnsBadRequest(string? path)
    {
        using var factory = new MediaProbeApplicationFactory();
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync(
            "/api/media/probe",
            new ProbeRequest(path));

        await AssertErrorAsync(
            response,
            HttpStatusCode.BadRequest,
            "invalid_path");
    }

    [Fact]
    public async Task Probe_MissingFile_ReturnsNotFound()
    {
        using var factory = new MediaProbeApplicationFactory();
        using var client = factory.CreateClient();
        var path = Path.Combine(factory.MediaRoot, "missing.mkv");

        using var response = await client.PostAsJsonAsync(
            "/api/media/probe",
            new ProbeRequest(path));

        await AssertErrorAsync(
            response,
            HttpStatusCode.NotFound,
            "file_not_found");
    }

    [Fact]
    public async Task Probe_ValidFile_ReturnsNormalizedPathAndSubtitles()
    {
        var subtitles = new[]
        {
            new SubtitleStream(2, "ass", "eng", "CR_English"),
            new SubtitleStream(3, "ass", "por", "CR_Portuguese(Brazil)")
        };
        using var factory = new MediaProbeApplicationFactory(
            (_, _) => Task.FromResult<IReadOnlyCollection<SubtitleStream>>(subtitles));
        var path = factory.CreateMediaFile(
            Path.Combine(
                "Séries",
                "Planetes",
                "[Erai-raws] Planetes - 01 [720p][MultiSub][BCFEB56F].mkv"));
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync(
            "/api/media/probe",
            new ProbeRequest(path));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<ProbeResponse>();
        Assert.NotNull(result);
        Assert.Equal(Path.GetFullPath(path), result.Path);
        Assert.Equal(subtitles, result.Subtitles);
    }

    [Fact]
    public async Task Probe_FileWithoutSubtitles_ReturnsEmptyCollection()
    {
        using var factory = new MediaProbeApplicationFactory();
        var path = factory.CreateMediaFile("video.mp4");
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync(
            "/api/media/probe",
            new ProbeRequest(path));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<ProbeResponse>();
        Assert.NotNull(result);
        Assert.Empty(result.Subtitles);
    }

    [Theory]
    [InlineData("{")]
    [InlineData("")]
    public async Task Probe_InvalidJson_ReturnsInvalidRequest(string payload)
    {
        using var factory = new MediaProbeApplicationFactory();
        using var client = factory.CreateClient();
        using var content = new StringContent(payload, Encoding.UTF8, "application/json");

        using var response = await client.PostAsync("/api/media/probe", content);

        await AssertErrorAsync(response, HttpStatusCode.BadRequest, "invalid_request");
    }

    [Fact]
    public async Task Probe_WrongContentType_ReturnsUnsupportedMediaType()
    {
        using var factory = new MediaProbeApplicationFactory();
        using var client = factory.CreateClient();
        using var content = new StringContent("{}", Encoding.UTF8, "text/plain");

        using var response = await client.PostAsync("/api/media/probe", content);

        await AssertErrorAsync(response, HttpStatusCode.UnsupportedMediaType, "unsupported_media_type");
    }

    [Fact]
    public async Task Probe_OversizedBody_ReturnsRequestTooLarge()
    {
        using var factory = new MediaProbeApplicationFactory();
        using var client = factory.CreateClient();
        using var content = new StringContent(
            "{\"path\":\"/media/" + new string('x', 17_000) + "\"}",
            Encoding.UTF8,
            "application/json");

        using var response = await client.PostAsync("/api/media/probe", content);

        await AssertErrorAsync(response, HttpStatusCode.RequestEntityTooLarge, "request_too_large");
    }

    [Fact]
    public async Task Probe_UnexpectedFailure_ReturnsGenericErrorInProduction()
    {
        using var factory = new MediaProbeApplicationFactory(
            (_, _) => Task.FromException<IReadOnlyCollection<SubtitleStream>>(
                new InvalidOperationException("secret technical detail")),
            "Production");
        var path = factory.CreateMediaFile("video.mkv");
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync(
            "/api/media/probe", new ProbeRequest(path));
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Contains("internal_error", body);
        Assert.DoesNotContain("secret technical detail", body);
        Assert.DoesNotContain(nameof(InvalidOperationException), body);
    }

    [Theory]
    [InlineData("failed", HttpStatusCode.UnprocessableEntity, "ffprobe_failed")]
    [InlineData("unavailable", HttpStatusCode.ServiceUnavailable, "ffprobe_unavailable")]
    [InlineData("timeout", HttpStatusCode.GatewayTimeout, "probe_timeout")]
    public async Task Probe_ControlledFfprobeFailure_ReturnsExpectedError(
        string failure,
        HttpStatusCode expectedStatusCode,
        string expectedError)
    {
        using var factory = new MediaProbeApplicationFactory(
            (_, _) => Task.FromException<IReadOnlyCollection<SubtitleStream>>(
                CreateException(failure)));
        var path = factory.CreateMediaFile("video.mkv");
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync(
            "/api/media/probe",
            new ProbeRequest(path));

        await AssertErrorAsync(response, expectedStatusCode, expectedError);
    }

    private static Exception CreateException(string failure) => failure switch
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
        HttpStatusCode expectedStatusCode,
        string expectedError)
    {
        Assert.Equal(expectedStatusCode, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);

        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.NotNull(error);
        Assert.Equal(expectedError, error.Error);
        Assert.False(string.IsNullOrWhiteSpace(error.Message));
    }

    private sealed class MediaProbeApplicationFactory : WebApplicationFactory<Program>
    {
        private readonly IFfprobeService _ffprobeService;
        private readonly string? _environment;

        public MediaProbeApplicationFactory(
            Func<string, CancellationToken, Task<IReadOnlyCollection<SubtitleStream>>>? handler = null,
            string? environment = null)
        {
            MediaRoot = Path.Combine(
                Path.GetTempPath(),
                $"mediatools-endpoint-tests-{Guid.NewGuid():N}");
            Directory.CreateDirectory(MediaRoot);
            _ffprobeService = new FakeFfprobeService(handler);
            _environment = environment;
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
            if (_environment is not null)
            {
                builder.UseEnvironment(_environment);
            }

            builder.ConfigureAppConfiguration((_, configuration) =>
            {
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["MediaTools:MediaRoot"] = MediaRoot,
                    ["MediaTools:FfprobePath"] = "ffprobe",
                    ["MediaTools:ProbeTimeoutSeconds"] = "30"
                });
            });

            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IFfprobeService>();
                services.AddSingleton(_ffprobeService);
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

    private sealed class FakeFfprobeService : IFfprobeService
    {
        private readonly Func<string, CancellationToken,
            Task<IReadOnlyCollection<SubtitleStream>>> _handler;

        public FakeFfprobeService(
            Func<string, CancellationToken,
                Task<IReadOnlyCollection<SubtitleStream>>>? handler)
        {
            _handler = handler ?? ((_, _) =>
                Task.FromResult<IReadOnlyCollection<SubtitleStream>>(
                    Array.Empty<SubtitleStream>()));
        }

        public Task<IReadOnlyCollection<SubtitleStream>> ProbeAsync(
            string path,
            CancellationToken cancellationToken) =>
            _handler(path, cancellationToken);
    }
}
