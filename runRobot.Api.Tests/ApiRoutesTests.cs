using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using System.Net;
using System.Net.Http.Json;

namespace runRobot.Api.Tests;

/// <summary>
/// Factory that injects a test API key bound to the current machine so the
/// ApiKeyMiddleware lets test requests through.
/// </summary>
public class TestApiFactory : WebApplicationFactory<Program>
{
    public const string TestKey = "test-api-key";

    protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ApiKeys:0:Key"]     = TestKey,
                ["ApiKeys:0:Machine"] = Environment.MachineName,
            });
        });
    }
}

public class ApiRoutesTests(TestApiFactory factory) : IClassFixture<TestApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task ScalarUi_Returns200()
    {
        var response = await _client.GetAsync("/scalar/v1");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task OpenApiJson_Returns200()
    {
        var response = await _client.GetAsync("/openapi/v1.json");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Analyze_NoApiKey_Returns401()
    {
        var response = await _client.PostAsJsonAsync("/analyze", new { videoPath = "C:\\fake\\video.mp4" });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task PostAnalyze_MissingVideoPath_ReturnsBadRequest()
    {
        _client.DefaultRequestHeaders.Remove("X-Api-Key");
        _client.DefaultRequestHeaders.Add("X-Api-Key", TestApiFactory.TestKey);
        var response = await _client.PostAsJsonAsync("/analyze", new { videoPath = "" });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetAnalyze_UnknownJobId_ReturnsNotFound()
    {
        _client.DefaultRequestHeaders.Remove("X-Api-Key");
        _client.DefaultRequestHeaders.Add("X-Api-Key", TestApiFactory.TestKey);
        var response = await _client.GetAsync($"/analyze/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task PostAnalyze_ValidVideoPath_ReturnsAccepted()
    {
        _client.DefaultRequestHeaders.Remove("X-Api-Key");
        _client.DefaultRequestHeaders.Add("X-Api-Key", TestApiFactory.TestKey);
        var response = await _client.PostAsJsonAsync("/analyze", new { videoPath = "C:\\fake\\video.mp4" });
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
    }
}
