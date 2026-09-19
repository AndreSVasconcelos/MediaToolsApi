using Microsoft.Extensions.Options;

namespace MediaTools.Api.Options;

public sealed class MediaToolsOptionsValidator : IValidateOptions<MediaToolsOptions>
{
    public ValidateOptionsResult Validate(
        string? name,
        MediaToolsOptions options)
    {
        var failures = new List<string>();

        if (string.IsNullOrWhiteSpace(options.MediaRoot) ||
            !Path.IsPathFullyQualified(options.MediaRoot))
        {
            failures.Add("MediaTools:MediaRoot must be a non-empty absolute path.");
        }
        else
        {
            try
            {
                _ = Path.GetFullPath(options.MediaRoot);
            }
            catch (Exception exception) when (
                exception is ArgumentException or NotSupportedException or PathTooLongException)
            {
                failures.Add("MediaTools:MediaRoot must be a valid filesystem path.");
            }
        }

        if (string.IsNullOrWhiteSpace(options.FfprobePath))
        {
            failures.Add("MediaTools:FfprobePath must not be empty.");
        }

        if (string.IsNullOrWhiteSpace(options.FfmpegPath))
        {
            failures.Add("MediaTools:FfmpegPath must not be empty.");
        }

        if (options.ProbeTimeoutSeconds <= 0)
        {
            failures.Add("MediaTools:ProbeTimeoutSeconds must be greater than zero.");
        }

        if (options.ExtractTimeoutSeconds <= 0)
        {
            failures.Add("MediaTools:ExtractTimeoutSeconds must be greater than zero.");
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }
}
