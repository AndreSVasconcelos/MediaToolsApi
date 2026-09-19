using MediaTools.Api.Options;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace MediaTools.Api.Tests;

public sealed class OptionsValidationTests
{
    [Fact]
    public void Validator_AcceptsValidAbsoluteConfiguration()
    {
        var result = Validate(new MediaToolsOptions
        {
            MediaRoot = "/media",
            FfprobePath = "ffprobe",
            FfmpegPath = "ffmpeg",
            ProbeTimeoutSeconds = 30,
            ExtractTimeoutSeconds = 120
        });

        Assert.True(result.Succeeded);
    }

    [Theory]
    [InlineData(nameof(MediaToolsOptions.MediaRoot))]
    [InlineData(nameof(MediaToolsOptions.FfprobePath))]
    [InlineData(nameof(MediaToolsOptions.FfmpegPath))]
    public void Validator_RejectsEmptyRequiredValues(string property)
    {
        var options = ValidOptions();

        typeof(MediaToolsOptions).GetProperty(property)!.SetValue(options, "");

        Assert.False(Validate(options).Succeeded);
    }

    [Fact]
    public void Validator_RejectsRelativeMediaRoot()
    {
        var options = new MediaToolsOptions
        {
            MediaRoot = "media",
            FfprobePath = "ffprobe",
            FfmpegPath = "ffmpeg",
            ProbeTimeoutSeconds = 30,
            ExtractTimeoutSeconds = 120
        };

        Assert.False(Validate(options).Succeeded);
    }

    [Theory]
    [InlineData(0, 120)]
    [InlineData(30, 0)]
    [InlineData(-1, 120)]
    public void Validator_RejectsNonPositiveTimeouts(int probeTimeout, int extractTimeout)
    {
        var options = new MediaToolsOptions
        {
            MediaRoot = "/media",
            FfprobePath = "ffprobe",
            FfmpegPath = "ffmpeg",
            ProbeTimeoutSeconds = probeTimeout,
            ExtractTimeoutSeconds = extractTimeout
        };

        Assert.False(Validate(options).Succeeded);
    }

    [Fact]
    public async Task StartupDiagnostics_DoesNotFailWhenMediaRootDoesNotExist()
    {
        var options = Microsoft.Extensions.Options.Options.Create(new MediaToolsOptions
        {
            MediaRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"))
        });
        var diagnostics = new MediaRootStartupDiagnostics(
            options,
            NullLogger<MediaRootStartupDiagnostics>.Instance);

        await diagnostics.StartAsync(CancellationToken.None);
    }

    private static ValidateOptionsResult Validate(MediaToolsOptions options) =>
        new MediaToolsOptionsValidator().Validate(
            Microsoft.Extensions.Options.Options.DefaultName,
            options);

    private static MediaToolsOptions ValidOptions() => new()
    {
        MediaRoot = "/media",
        FfprobePath = "ffprobe",
        FfmpegPath = "ffmpeg",
        ProbeTimeoutSeconds = 30,
        ExtractTimeoutSeconds = 120
    };
}
