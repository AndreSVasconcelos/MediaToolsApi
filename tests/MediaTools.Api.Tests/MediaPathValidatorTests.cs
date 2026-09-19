using MediaTools.Api.Options;
using MediaTools.Api.Services;

namespace MediaTools.Api.Tests;

public sealed class MediaPathValidatorTests : IDisposable
{
    private readonly string _mediaRoot;
    private readonly MediaPathValidator _validator;

    public MediaPathValidatorTests()
    {
        _mediaRoot = Path.Combine(
            Path.GetTempPath(),
            $"mediatools-path-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_mediaRoot);

        _validator = new MediaPathValidator(
            Microsoft.Extensions.Options.Options.Create(new MediaToolsOptions
            {
                MediaRoot = _mediaRoot
            }));
    }

    [Fact]
    public void Validate_FileAtMediaRoot_ReturnsNormalizedPath()
    {
        var path = CreateFile("video.mkv");

        var result = _validator.Validate(path);

        Assert.Equal(MediaPathValidationError.None, result.Error);
        Assert.Equal(Path.GetFullPath(path), result.FullPath);
    }

    [Fact]
    public void Validate_FileInNestedDirectory_ReturnsNormalizedPath()
    {
        var path = CreateFile(Path.Combine("Séries", "Teste", "video.mkv"));

        var result = _validator.Validate(path);

        Assert.Equal(MediaPathValidationError.None, result.Error);
        Assert.Equal(Path.GetFullPath(path), result.FullPath);
    }

    [Theory]
    [InlineData("video.mkv")]
    [InlineData("/etc/passwd")]
    public void Validate_PathOutsideMediaRoot_ReturnsInvalidPath(string path)
    {
        var result = _validator.Validate(path);

        Assert.Equal(MediaPathValidationError.InvalidPath, result.Error);
    }

    [Fact]
    public void Validate_ParentTraversalOutsideMediaRoot_ReturnsInvalidPath()
    {
        var path = Path.Combine(_mediaRoot, "..", "outside.mkv");

        var result = _validator.Validate(path);

        Assert.Equal(MediaPathValidationError.InvalidPath, result.Error);
    }

    [Fact]
    public void Validate_ParentTraversalRemainingInsideMediaRoot_ReturnsNormalizedPath()
    {
        var expectedPath = CreateFile("video.mkv");
        var nestedDirectory = Directory.CreateDirectory(
            Path.Combine(_mediaRoot, "nested"));
        var path = Path.Combine(nestedDirectory.FullName, "..", "video.mkv");

        var result = _validator.Validate(path);

        Assert.Equal(MediaPathValidationError.None, result.Error);
        Assert.Equal(expectedPath, result.FullPath);
    }

    [Fact]
    public void Validate_SiblingWithSamePrefix_ReturnsInvalidPath()
    {
        var path = Path.Combine($"{_mediaRoot}-other", "video.mkv");

        var result = _validator.Validate(path);

        Assert.Equal(MediaPathValidationError.InvalidPath, result.Error);
    }

    [Fact]
    public void Validate_Directory_ReturnsInvalidPath()
    {
        var directory = Directory.CreateDirectory(
            Path.Combine(_mediaRoot, "directory"));

        var result = _validator.Validate(directory.FullName);

        Assert.Equal(MediaPathValidationError.InvalidPath, result.Error);
    }

    [Fact]
    public void Validate_MissingFileInsideMediaRoot_ReturnsFileNotFound()
    {
        var path = Path.Combine(_mediaRoot, "missing.mkv");

        var result = _validator.Validate(path);

        Assert.Equal(MediaPathValidationError.FileNotFound, result.Error);
    }

    [Fact]
    public void Validate_FileSymbolicLink_ReturnsInvalidPath()
    {
        var target = CreateFile("target.mkv");
        var link = Path.Combine(_mediaRoot, "linked.mkv");
        File.CreateSymbolicLink(link, target);

        var result = _validator.Validate(link);

        Assert.Equal(MediaPathValidationError.InvalidPath, result.Error);
    }

    [Fact]
    public void Validate_IntermediateDirectorySymbolicLink_ReturnsInvalidPath()
    {
        var targetDirectory = Directory.CreateDirectory(
            Path.Combine(_mediaRoot, "target-directory"));
        File.WriteAllText(Path.Combine(targetDirectory.FullName, "video.mkv"), string.Empty);

        var linkedDirectory = Path.Combine(_mediaRoot, "linked-directory");
        Directory.CreateSymbolicLink(linkedDirectory, targetDirectory.FullName);

        var result = _validator.Validate(Path.Combine(linkedDirectory, "video.mkv"));

        Assert.Equal(MediaPathValidationError.InvalidPath, result.Error);
    }

    [Fact]
    public void Validate_SymbolicLinkConfiguredAsMediaRoot_ReturnsInvalidPath()
    {
        var target = CreateFile("video.mkv");
        var linkedRoot = $"{_mediaRoot}-link";
        Directory.CreateSymbolicLink(linkedRoot, _mediaRoot);

        try
        {
            var validator = new MediaPathValidator(
                Microsoft.Extensions.Options.Options.Create(new MediaToolsOptions
                {
                    MediaRoot = linkedRoot
                }));

            var result = validator.Validate(
                Path.Combine(linkedRoot, Path.GetFileName(target)));

            Assert.Equal(MediaPathValidationError.InvalidPath, result.Error);
        }
        finally
        {
            Directory.Delete(linkedRoot);
        }
    }

    [Fact]
    public void ValidateDestination_MissingPathInsideMediaRoot_ReturnsNormalizedPath()
    {
        var path = Path.Combine(_mediaRoot, "subtitle.pt-BR.srt");

        var result = _validator.ValidateDestination(path);

        Assert.Equal(MediaPathValidationError.None, result.Error);
        Assert.Equal(Path.GetFullPath(path), result.FullPath);
    }

    [Fact]
    public void ValidateDestination_ExistingFile_ReturnsDestinationExists()
    {
        var path = CreateFile("subtitle.pt-BR.srt");

        var result = _validator.ValidateDestination(path);

        Assert.Equal(MediaPathValidationError.DestinationExists, result.Error);
        Assert.Equal(path, result.FullPath);
    }

    [Fact]
    public void ValidateDestination_ExistingSymbolicLink_ReturnsDestinationExists()
    {
        var target = CreateFile("existing.srt");
        var link = Path.Combine(_mediaRoot, "subtitle.pt-BR.srt");
        File.CreateSymbolicLink(link, target);

        var result = _validator.ValidateDestination(link);

        Assert.Equal(MediaPathValidationError.DestinationExists, result.Error);
    }

    [Fact]
    public void ValidateDestination_ParentSymbolicLink_ReturnsInvalidPath()
    {
        var targetDirectory = Directory.CreateDirectory(
            Path.Combine(_mediaRoot, "destination-target"));
        var linkedDirectory = Path.Combine(_mediaRoot, "destination-link");
        Directory.CreateSymbolicLink(linkedDirectory, targetDirectory.FullName);

        var result = _validator.ValidateDestination(
            Path.Combine(linkedDirectory, "subtitle.srt"));

        Assert.Equal(MediaPathValidationError.InvalidPath, result.Error);
    }

    [Fact]
    public void ValidateDestination_PathOutsideMediaRoot_ReturnsInvalidPath()
    {
        var path = Path.Combine($"{_mediaRoot}-other", "subtitle.srt");

        var result = _validator.ValidateDestination(path);

        Assert.Equal(MediaPathValidationError.InvalidPath, result.Error);
    }

    public void Dispose()
    {
        if (Directory.Exists(_mediaRoot))
        {
            Directory.Delete(_mediaRoot, recursive: true);
        }
    }

    private string CreateFile(string relativePath)
    {
        var fullPath = Path.Combine(_mediaRoot, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllText(fullPath, string.Empty);
        return fullPath;
    }
}
