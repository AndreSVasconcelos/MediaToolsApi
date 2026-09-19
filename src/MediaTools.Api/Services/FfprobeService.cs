using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;
using MediaTools.Api.Models;
using MediaTools.Api.Options;
using Microsoft.Extensions.Options;

namespace MediaTools.Api.Services;

public sealed class FfprobeService : IFfprobeService
{
    private readonly MediaToolsOptions _options;
    private readonly ILogger<FfprobeService> _logger;

    public FfprobeService(
        IOptions<MediaToolsOptions> options,
        ILogger<FfprobeService> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task<IReadOnlyCollection<SubtitleStream>> ProbeAsync(
        string path,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        _logger.LogInformation("Starting ffprobe for validated media path {MediaPath}.", path);

        using var process = new Process
        {
            StartInfo = CreateStartInfo(path)
        };

        try
        {
            if (!process.Start())
            {
                throw new InvalidOperationException("The ffprobe process did not start.");
            }
        }
        catch (Exception exception) when (
            exception is Win32Exception or InvalidOperationException)
        {
            _logger.LogError(exception, "Unable to start ffprobe.");
            throw new FfprobeUnavailableException(
                "The ffprobe executable could not be started.",
                exception);
        }

        var standardOutputTask = process.StandardOutput.ReadToEndAsync();
        var standardErrorTask = process.StandardError.ReadToEndAsync();

        using var timeoutCancellation = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken);
        timeoutCancellation.CancelAfter(TimeSpan.FromSeconds(_options.ProbeTimeoutSeconds));

        try
        {
            await process.WaitForExitAsync(timeoutCancellation.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            await TerminateProcessAsync(process);
            await Task.WhenAll(standardOutputTask, standardErrorTask);

            _logger.LogWarning(
                "ffprobe timed out after {TimeoutSeconds} seconds for {MediaPath}.",
                _options.ProbeTimeoutSeconds,
                path);

            throw new FfprobeTimeoutException("ffprobe exceeded the configured timeout.");
        }
        catch (OperationCanceledException)
        {
            await TerminateProcessAsync(process);
            await Task.WhenAll(standardOutputTask, standardErrorTask);
            throw;
        }

        var standardOutput = await standardOutputTask;
        var standardError = await standardErrorTask;

        if (process.ExitCode != 0)
        {
            _logger.LogWarning(
                "ffprobe exited with code {ExitCode} for {MediaPath}. Stderr: {StandardError}",
                process.ExitCode,
                path,
                standardError);

            throw new FfprobeFailedException("ffprobe returned a non-zero exit code.");
        }

        try
        {
            return FfprobeOutputParser.Parse(standardOutput);
        }
        catch (JsonException exception)
        {
            _logger.LogError(exception, "ffprobe returned invalid JSON for {MediaPath}.", path);
            throw new FfprobeFailedException("ffprobe returned invalid JSON.", exception);
        }
    }

    private ProcessStartInfo CreateStartInfo(string path)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = _options.FfprobePath,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        startInfo.ArgumentList.Add("-v");
        startInfo.ArgumentList.Add("error");
        startInfo.ArgumentList.Add("-select_streams");
        startInfo.ArgumentList.Add("s");
        startInfo.ArgumentList.Add("-show_entries");
        startInfo.ArgumentList.Add("stream=index,codec_name:stream_tags=language,title");
        startInfo.ArgumentList.Add("-of");
        startInfo.ArgumentList.Add("json");
        startInfo.ArgumentList.Add(path);

        return startInfo;
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
