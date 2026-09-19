using MediaTools.Api.Models;
using System.Collections.Concurrent;

namespace MediaTools.Api.Services;

public sealed class SubtitleExtractionService : ISubtitleExtractionService
{
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> DestinationLocks = new(StringComparer.Ordinal);
    private readonly IFfprobeService _ffprobeService;
    private readonly IFfmpegSubtitleExtractor _ffmpegExtractor;
    private readonly IMediaPathValidator _pathValidator;
    private readonly ILogger<SubtitleExtractionService> _logger;

    public SubtitleExtractionService(
        IFfprobeService ffprobeService,
        IFfmpegSubtitleExtractor ffmpegExtractor,
        IMediaPathValidator pathValidator,
        ILogger<SubtitleExtractionService> logger)
    {
        _ffprobeService = ffprobeService;
        _ffmpegExtractor = ffmpegExtractor;
        _pathValidator = pathValidator;
        _logger = logger;
    }

    public async Task<ExtractSubtitleResponse> ExtractAsync(
        string sourcePath,
        int streamIndex,
        string targetLanguage,
        CancellationToken cancellationToken)
    {
        var streams = await _ffprobeService.ProbeAsync(sourcePath, cancellationToken);
        var stream = streams.SingleOrDefault(candidate => candidate.Index == streamIndex)
            ?? throw new SubtitleStreamNotFoundException();

        if (!SubtitleExtractionPolicy.IsSupportedCodec(stream.Codec))
        {
            throw new UnsupportedSubtitleCodecException();
        }

        var outputPath = SubtitleExtractionPolicy.BuildOutputPath(
            sourcePath,
            targetLanguage);
        var destinationLock = DestinationLocks.GetOrAdd(
            outputPath,
            static _ => new SemaphoreSlim(1, 1));
        await destinationLock.WaitAsync(cancellationToken);

        try
        {
            return await ExtractToDestinationAsync(
                sourcePath,
                streamIndex,
                targetLanguage,
                stream,
                outputPath,
                cancellationToken);
        }
        finally
        {
            destinationLock.Release();
            DestinationLocks.TryRemove(
                new KeyValuePair<string, SemaphoreSlim>(outputPath, destinationLock));
        }
    }

    private async Task<ExtractSubtitleResponse> ExtractToDestinationAsync(
        string sourcePath,
        int streamIndex,
        string targetLanguage,
        SubtitleStream stream,
        string outputPath,
        CancellationToken cancellationToken)
    {
        EnsureDestinationIsAvailable(outputPath);
        var temporaryOutputPath = CreateTemporaryOutputPath(outputPath);

        _logger.LogInformation(
            "Starting subtitle extraction from {SourcePath}, stream {StreamIndex}, codec " +
            "{Codec}, target language {TargetLanguage}, output {OutputPath}.",
            sourcePath,
            streamIndex,
            stream.Codec,
            targetLanguage,
            outputPath);

        try
        {
            await _ffmpegExtractor.ExtractAsync(
                sourcePath,
                streamIndex,
                temporaryOutputPath,
                cancellationToken);

            var temporaryFile = new FileInfo(temporaryOutputPath);

            if (!temporaryFile.Exists ||
                temporaryFile.LinkTarget is not null ||
                temporaryFile.Length == 0)
            {
                throw new FfmpegFailedException(
                    "ffmpeg did not produce a non-empty regular subtitle file.");
            }

            EnsureDestinationIsAvailable(outputPath);

            try
            {
                File.Move(temporaryOutputPath, outputPath, overwrite: false);
            }
            catch (IOException) when (FileSystemEntryExists(outputPath))
            {
                throw new SubtitleAlreadyExistsException();
            }

            var writtenBytes = new FileInfo(outputPath).Length;

            _logger.LogInformation(
                "Subtitle extraction completed for {SourcePath}. Output {OutputPath}, " +
                "{WrittenBytes} bytes written.",
                sourcePath,
                outputPath,
                writtenBytes);

            return new ExtractSubtitleResponse(
                sourcePath,
                streamIndex,
                stream.Codec,
                stream.Language,
                targetLanguage,
                outputPath,
                writtenBytes);
        }
        finally
        {
            DeleteTemporaryOutput(temporaryOutputPath);
        }
    }

    private void EnsureDestinationIsAvailable(string outputPath)
    {
        var validation = _pathValidator.ValidateDestination(outputPath);

        if (validation.Error == MediaPathValidationError.DestinationExists)
        {
            throw new SubtitleAlreadyExistsException();
        }

        if (validation.Error != MediaPathValidationError.None)
        {
            throw new InvalidSubtitleOutputPathException();
        }
    }

    private string CreateTemporaryOutputPath(string outputPath)
    {
        var temporaryOutputPath = $"{outputPath}.{Guid.NewGuid():N}.tmp";
        var validation = _pathValidator.ValidateDestination(temporaryOutputPath);

        if (validation.Error != MediaPathValidationError.None)
        {
            throw new InvalidSubtitleOutputPathException();
        }

        return temporaryOutputPath;
    }

    private void DeleteTemporaryOutput(string temporaryOutputPath)
    {
        try
        {
            File.Delete(temporaryOutputPath);
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning(
                exception,
                "Unable to remove temporary subtitle file {TemporaryOutputPath}.",
                temporaryOutputPath);
        }
    }

    private static bool FileSystemEntryExists(string path)
    {
        if (File.Exists(path) || Directory.Exists(path))
        {
            return true;
        }

        try
        {
            return new FileInfo(path).LinkTarget is not null;
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            return true;
        }
    }
}
