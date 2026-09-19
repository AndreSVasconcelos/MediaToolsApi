using System.ComponentModel;
using System.Diagnostics;
using MediaTools.Api.Options;
using Microsoft.Extensions.Options;

namespace MediaTools.Api.Services;

public sealed class FfmpegSubtitleExtractor : IFfmpegSubtitleExtractor
{
    private readonly MediaToolsOptions _options;
    private readonly ILogger<FfmpegSubtitleExtractor> _logger;

    public FfmpegSubtitleExtractor(
        IOptions<MediaToolsOptions> options,
        ILogger<FfmpegSubtitleExtractor> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task ExtractAsync(
        string sourcePath,
        int streamIndex,
        string temporaryOutputPath,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        using var process = new Process
        {
            StartInfo = FfmpegProcessStartInfoFactory.Create(
                _options.FfmpegPath,
                sourcePath,
                streamIndex,
                temporaryOutputPath)
        };

        try
        {
            if (!process.Start())
            {
                throw new InvalidOperationException("The ffmpeg process did not start.");
            }
        }
        catch (Exception exception) when (
            exception is Win32Exception or InvalidOperationException)
        {
            _logger.LogError(exception, "Unable to start ffmpeg.");
            throw new FfmpegUnavailableException(
                "The ffmpeg executable could not be started.",
                exception);
        }

        var standardOutputTask = process.StandardOutput.ReadToEndAsync();
        var standardErrorTask = process.StandardError.ReadToEndAsync();

        using var timeoutCancellation = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken);
        timeoutCancellation.CancelAfter(TimeSpan.FromSeconds(_options.ExtractTimeoutSeconds));

        try
        {
            await process.WaitForExitAsync(timeoutCancellation.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            await TerminateProcessAsync(process);
            await Task.WhenAll(standardOutputTask, standardErrorTask);

            _logger.LogWarning(
                "ffmpeg timed out after {TimeoutSeconds} seconds for {SourcePath}.",
                _options.ExtractTimeoutSeconds,
                sourcePath);

            throw new FfmpegTimeoutException("ffmpeg exceeded the configured timeout.");
        }
        catch (OperationCanceledException)
        {
            await TerminateProcessAsync(process);
            await Task.WhenAll(standardOutputTask, standardErrorTask);
            throw;
        }

        await standardOutputTask;
        var standardError = await standardErrorTask;

        if (process.ExitCode != 0)
        {
            _logger.LogWarning(
                "ffmpeg exited with code {ExitCode} for {SourcePath}. Stderr: {StandardError}",
                process.ExitCode,
                sourcePath,
                standardError);

            throw new FfmpegFailedException("ffmpeg returned a non-zero exit code.");
        }
    }

    private static async Task TerminateProcessAsync(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }

            await process.WaitForExitAsync(CancellationToken.None);
        }
        catch (InvalidOperationException)
        {
            // The process already exited between the state check and termination.
        }
    }
}
