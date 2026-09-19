using MediaTools.Api.Services;

namespace MediaTools.Api.Tests;

public sealed class FfmpegProcessStartInfoFactoryTests
{
    [Fact]
    public void Create_UsesArgumentListWithoutShellAndExplicitSrtFormat()
    {
        const string sourcePath = "/media/Planetes/[Erai-raws] Episode 01.mkv";
        const string temporaryPath = "/media/Planetes/Episode 01.pt-BR.srt.abc.tmp";

        var result = FfmpegProcessStartInfoFactory.Create(
            "/usr/bin/ffmpeg",
            sourcePath,
            3,
            temporaryPath);

        Assert.Equal("/usr/bin/ffmpeg", result.FileName);
        Assert.False(result.UseShellExecute);
        Assert.True(result.RedirectStandardOutput);
        Assert.True(result.RedirectStandardError);
        Assert.Equal(
            [
                "-v",
                "error",
                "-nostdin",
                "-n",
                "-i",
                sourcePath,
                "-map",
                "0:3",
                "-c:s",
                "srt",
                "-f",
                "srt",
                temporaryPath
            ],
            result.ArgumentList);
    }
}
