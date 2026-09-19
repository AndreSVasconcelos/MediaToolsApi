using System.Diagnostics;

namespace MediaTools.Api.Services;

internal static class FfmpegProcessStartInfoFactory
{
    public static ProcessStartInfo Create(
        string ffmpegPath,
        string sourcePath,
        int streamIndex,
        string temporaryOutputPath)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = ffmpegPath,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        startInfo.ArgumentList.Add("-v");
        startInfo.ArgumentList.Add("error");
        startInfo.ArgumentList.Add("-nostdin");
        startInfo.ArgumentList.Add("-n");
        startInfo.ArgumentList.Add("-i");
        startInfo.ArgumentList.Add(sourcePath);
        startInfo.ArgumentList.Add("-map");
        startInfo.ArgumentList.Add($"0:{streamIndex}");
        startInfo.ArgumentList.Add("-c:s");
        startInfo.ArgumentList.Add("srt");
        startInfo.ArgumentList.Add("-f");
        startInfo.ArgumentList.Add("srt");
        startInfo.ArgumentList.Add(temporaryOutputPath);

        return startInfo;
    }
}
