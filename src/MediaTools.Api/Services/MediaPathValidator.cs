using MediaTools.Api.Options;
using Microsoft.Extensions.Options;

namespace MediaTools.Api.Services;

public sealed class MediaPathValidator : IMediaPathValidator
{
    private readonly string _mediaRoot;

    public MediaPathValidator(IOptions<MediaToolsOptions> options)
    {
        _mediaRoot = Path.TrimEndingDirectorySeparator(
            Path.GetFullPath(options.Value.MediaRoot));
    }

    public MediaPathValidationResult Validate(string? path)
    {
        var candidate = ValidateCandidate(path);

        if (candidate.Error != MediaPathValidationError.None)
        {
            return candidate;
        }

        var fullPath = candidate.FullPath!;

        if (Directory.Exists(fullPath))
        {
            return MediaPathValidationResult.Failure(MediaPathValidationError.InvalidPath);
        }

        if (!File.Exists(fullPath))
        {
            return MediaPathValidationResult.Failure(MediaPathValidationError.FileNotFound);
        }

        return candidate;
    }

    public MediaPathValidationResult ValidateDestination(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path))
        {
            return MediaPathValidationResult.Failure(MediaPathValidationError.InvalidPath);
        }

        string fullPath;

        try
        {
            fullPath = Path.GetFullPath(path);
        }
        catch (Exception exception) when (
            exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return MediaPathValidationResult.Failure(MediaPathValidationError.InvalidPath);
        }

        if (!IsWithinMediaRoot(fullPath))
        {
            return MediaPathValidationResult.Failure(MediaPathValidationError.InvalidPath);
        }

        if (FileSystemEntryExists(fullPath))
        {
            return new MediaPathValidationResult(
                fullPath,
                MediaPathValidationError.DestinationExists);
        }

        return ContainsSymbolicLink(fullPath)
            ? MediaPathValidationResult.Failure(MediaPathValidationError.InvalidPath)
            : MediaPathValidationResult.Success(fullPath);
    }

    private MediaPathValidationResult ValidateCandidate(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path))
        {
            return MediaPathValidationResult.Failure(MediaPathValidationError.InvalidPath);
        }

        try
        {
            var fullPath = Path.GetFullPath(path);

            return IsWithinMediaRoot(fullPath) && !ContainsSymbolicLink(fullPath)
                ? MediaPathValidationResult.Success(fullPath)
                : MediaPathValidationResult.Failure(MediaPathValidationError.InvalidPath);
        }
        catch (Exception exception) when (
            exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return MediaPathValidationResult.Failure(MediaPathValidationError.InvalidPath);
        }
    }

    private bool IsWithinMediaRoot(string fullPath)
    {
        var relativePath = Path.GetRelativePath(_mediaRoot, fullPath);

        return relativePath != ".." &&
               !relativePath.StartsWith(
                   $"..{Path.DirectorySeparatorChar}",
                   StringComparison.Ordinal) &&
               !Path.IsPathFullyQualified(relativePath);
    }

    private static bool ContainsSymbolicLink(string fullPath)
    {
        var root = Path.GetPathRoot(fullPath);

        if (string.IsNullOrEmpty(root))
        {
            return true;
        }

        var currentPath = root;
        var segments = fullPath[root.Length..]
            .Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries);

        foreach (var segment in segments)
        {
            currentPath = Path.Combine(currentPath, segment);

            try
            {
                FileSystemInfo entry = Directory.Exists(currentPath)
                    ? new DirectoryInfo(currentPath)
                    : new FileInfo(currentPath);

                if (entry.LinkTarget is not null)
                {
                    return true;
                }
            }
            catch (Exception exception) when (
                exception is IOException or UnauthorizedAccessException or NotSupportedException)
            {
                return true;
            }
        }

        return false;
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
