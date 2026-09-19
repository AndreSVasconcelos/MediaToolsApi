namespace MediaTools.Api.Services;

public interface IMediaPathValidator
{
    MediaPathValidationResult Validate(string? path);

    MediaPathValidationResult ValidateDestination(string? path);
}

public enum MediaPathValidationError
{
    None,
    InvalidPath,
    FileNotFound,
    DestinationExists
}

public sealed record MediaPathValidationResult(
    string? FullPath,
    MediaPathValidationError Error)
{
    public static MediaPathValidationResult Success(string fullPath) =>
        new(fullPath, MediaPathValidationError.None);

    public static MediaPathValidationResult Failure(MediaPathValidationError error) =>
        new(null, error);
}
