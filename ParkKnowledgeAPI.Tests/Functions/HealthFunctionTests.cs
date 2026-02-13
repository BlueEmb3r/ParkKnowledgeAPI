using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using ParkKnowledgeAPI.Functions;
using ParkKnowledgeAPI.Tests.Helpers;
using Qdrant.Client;

namespace ParkKnowledgeAPI.Tests.Functions;

public class HealthFunctionTests
{
    private readonly Mock<ILogger<HealthFunction>> _loggerMock = new();

    /// <summary>
    /// Creates a QdrantClient pointing at a non-existent host so ListCollections will throw.
    /// </summary>
    private static QdrantClient CreateUnreachableQdrantClient()
    {
        return new QdrantClient("localhost", port: 1); // Port 1 — nothing listening
    }

    private static IConfiguration CreateConfiguration(bool withDeepSeek)
    {
        var data = new Dictionary<string, string?>();
        if (withDeepSeek)
        {
            data["DeepSeek:ApiKey"] = "sk-test-key-12345";
            data["DeepSeek:Endpoint"] = "https://api.deepseek.com";
            data["DeepSeek:ModelId"] = "deepseek-chat";
        }
        return new ConfigurationBuilder().AddInMemoryCollection(data).Build();
    }

    [Fact]
    public async Task Run_QdrantUnreachable_ReturnsDegradedStatus()
    {
        var client = CreateUnreachableQdrantClient();
        var config = CreateConfiguration(withDeepSeek: true);
        var sut = new HealthFunction(client, config, _loggerMock.Object);
        var req = HttpRequestHelper.CreateEmptyRequest();

        var result = await sut.Run(req);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var json = JsonSerializer.Serialize(okResult.Value);
        Assert.Contains("\"status\":\"degraded\"", json);
    }

    [Fact]
    public async Task Run_QdrantUnreachable_DeepSeekConfigured_DeepSeekHealthy()
    {
        var client = CreateUnreachableQdrantClient();
        var config = CreateConfiguration(withDeepSeek: true);
        var sut = new HealthFunction(client, config, _loggerMock.Object);
        var req = HttpRequestHelper.CreateEmptyRequest();

        var result = await sut.Run(req);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var json = JsonSerializer.Serialize(okResult.Value);

        // Qdrant is unhealthy, but DeepSeek is healthy (config-only check)
        using var doc = JsonDocument.Parse(json);
        var deepSeekStatus = doc.RootElement.GetProperty("modules").GetProperty("deepSeek").GetProperty("Status").GetString();
        Assert.Equal("healthy", deepSeekStatus);
    }

    [Fact]
    public async Task Run_QdrantUnreachable_DeepSeekNotConfigured_BothUnhealthy()
    {
        var client = CreateUnreachableQdrantClient();
        var config = CreateConfiguration(withDeepSeek: false);
        var sut = new HealthFunction(client, config, _loggerMock.Object);
        var req = HttpRequestHelper.CreateEmptyRequest();

        var result = await sut.Run(req);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var json = JsonSerializer.Serialize(okResult.Value);
        Assert.Contains("\"status\":\"degraded\"", json);

        using var doc = JsonDocument.Parse(json);
        var deepSeekStatus = doc.RootElement.GetProperty("modules").GetProperty("deepSeek").GetProperty("Status").GetString();
        Assert.Equal("unhealthy", deepSeekStatus);
    }
}
