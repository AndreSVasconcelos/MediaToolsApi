using System.Text.Json;
using System.Text.Json.Serialization;
using MediaTools.Api.Models;

namespace MediaTools.Api.Services;

internal static class FfprobeOutputParser
{
    public static IReadOnlyCollection<SubtitleStream> Parse(string json)
    {
        var output = JsonSerializer.Deserialize<FfprobeOutput>(json)
            ?? throw new JsonException("ffprobe returned an empty JSON document.");

        if (output.Streams is null || output.Streams.Count == 0)
        {
            return Array.Empty<SubtitleStream>();
        }

        return output.Streams
            .Select(stream => new SubtitleStream(
                stream.Index,
                stream.CodecName,
                stream.Tags?.Language,
                stream.Tags?.Title))
            .ToArray();
    }

    private sealed class FfprobeOutput
    {
        [JsonPropertyName("streams")]
        public List<FfprobeStream>? Streams { get; init; }
    }

    private sealed class FfprobeStream
    {
        [JsonPropertyName("index")]
        public int Index { get; init; }

        [JsonPropertyName("codec_name")]
        public string? CodecName { get; init; }

        [JsonPropertyName("tags")]
        public FfprobeTags? Tags { get; init; }
    }

    private sealed class FfprobeTags
    {
        [JsonPropertyName("language")]
        public string? Language { get; init; }

        [JsonPropertyName("title")]
        public string? Title { get; init; }
    }
}
