using System.Text.Json;
using MediaTools.Api.Services;

namespace MediaTools.Api.Tests;

public sealed class FfprobeOutputParserTests
{
    [Fact]
    public void Parse_StreamsWithOptionalFields_MapsSubtitleMetadata()
    {
        const string json = """
            {
              "streams": [
                {
                  "index": 2,
                  "codec_name": "ass",
                  "tags": {
                    "language": "por",
                    "title": "CR_Portuguese(Brazil)"
                  }
                },
                {
                  "index": 3,
                  "codec_name": "subrip",
                  "tags": {
                    "language": "eng"
                  }
                },
                {
                  "index": 4
                }
              ]
            }
            """;

        var result = FfprobeOutputParser.Parse(json).ToArray();

        Assert.Equal(3, result.Length);
        Assert.Equal((2, "ass", "por", "CR_Portuguese(Brazil)"),
            (result[0].Index, result[0].Codec, result[0].Language, result[0].Title));
        Assert.Equal((3, "subrip", "eng", null),
            (result[1].Index, result[1].Codec, result[1].Language, result[1].Title));
        Assert.Equal((4, null, null, null),
            (result[2].Index, result[2].Codec, result[2].Language, result[2].Title));
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"streams\":[]}")]
    public void Parse_NoStreams_ReturnsEmptyCollection(string json)
    {
        var result = FfprobeOutputParser.Parse(json);

        Assert.Empty(result);
    }

    [Fact]
    public void Parse_InvalidJson_ThrowsJsonException()
    {
        Assert.Throws<JsonException>(() => FfprobeOutputParser.Parse("not-json"));
    }
}
