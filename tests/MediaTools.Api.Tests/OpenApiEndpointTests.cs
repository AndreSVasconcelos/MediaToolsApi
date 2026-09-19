using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace MediaTools.Api.Tests;

public sealed class OpenApiEndpointTests
{
    [Theory]
    [InlineData("Development", HttpStatusCode.OK)]
    [InlineData("Production", HttpStatusCode.NotFound)]
    public async Task OpenApiAvailability_MatchesEnvironment(
        string environment,
        HttpStatusCode expectedStatusCode)
    {
        using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder => builder.UseEnvironment(environment));
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/openapi/v1.json");

        Assert.Equal(expectedStatusCode, response.StatusCode);

        if (expectedStatusCode == HttpStatusCode.OK)
        {
            Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var paths = document.RootElement.GetProperty("paths");
            Assert.True(paths.TryGetProperty("/api/media/probe", out var probe));
            Assert.True(probe.GetProperty("post").GetProperty("requestBody").GetProperty("content")
                .TryGetProperty("application/json", out _));
            Assert.True(probe.GetProperty("post").GetProperty("responses")
                .TryGetProperty("413", out _));
            Assert.True(paths.TryGetProperty("/api/media/extract-subtitle", out var extract));
            Assert.True(extract.GetProperty("post").GetProperty("responses")
                .TryGetProperty("504", out _));
        }
    }
}
