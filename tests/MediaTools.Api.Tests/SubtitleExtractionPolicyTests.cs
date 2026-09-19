using MediaTools.Api.Services;

namespace MediaTools.Api.Tests;

public sealed class SubtitleExtractionPolicyTests
{
    [Theory]
    [InlineData("pt-BR")]
    [InlineData("pt")]
    [InlineData("en")]
    [InlineData("eng")]
    [InlineData("PT-br")]
    public void IsValidTargetLanguage_ValidValue_ReturnsTrue(string targetLanguage)
    {
        Assert.True(SubtitleExtractionPolicy.IsValidTargetLanguage(targetLanguage));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("../pt-BR")]
    [InlineData("pt/BR")]
    [InlineData("pt\\BR")]
    [InlineData("pt BR")]
    [InlineData("p")]
    [InlineData("portuguese")]
    public void IsValidTargetLanguage_InvalidValue_ReturnsFalse(string? targetLanguage)
    {
        Assert.False(SubtitleExtractionPolicy.IsValidTargetLanguage(targetLanguage));
    }

    [Theory]
    [InlineData("subrip")]
    [InlineData("ass")]
    [InlineData("ssa")]
    [InlineData("webvtt")]
    [InlineData("mov_text")]
    [InlineData("ASS")]
    public void IsSupportedCodec_TextCodec_ReturnsTrue(string codec)
    {
        Assert.True(SubtitleExtractionPolicy.IsSupportedCodec(codec));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("hdmv_pgs_subtitle")]
    [InlineData("dvd_subtitle")]
    [InlineData("dvb_subtitle")]
    public void IsSupportedCodec_UnsupportedCodec_ReturnsFalse(string? codec)
    {
        Assert.False(SubtitleExtractionPolicy.IsSupportedCodec(codec));
    }

    [Theory]
    [InlineData("/video/Test.mkv", "pt-BR", "/video/Test.pt-BR.srt")]
    [InlineData("/video/My Episode 01.mkv", "en", "/video/My Episode 01.en.srt")]
    [InlineData(
        "/video/[Erai-raws] Planetes - 01 [720p].mkv",
        "pt-BR",
        "/video/[Erai-raws] Planetes - 01 [720p].pt-BR.srt")]
    public void BuildOutputPath_PreservesBaseName(
        string sourcePath,
        string targetLanguage,
        string expectedPath)
    {
        var result = SubtitleExtractionPolicy.BuildOutputPath(
            sourcePath,
            targetLanguage);

        Assert.Equal(expectedPath, result);
    }
}
