using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Qdrant.Client;

namespace ParkKnowledgeAPI.Functions;

public class HealthFunction
{
    private readonly QdrantClient _qdrantClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<HealthFunction> _logger;

    public HealthFunction(QdrantClient qdrantClient, IConfiguration configuration, ILogger<HealthFunction> logger)
    {
        _qdrantClient = qdrantClient;
        _configuration = configuration;
        _logger = logger;
    }

    [Function("Health")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/health")] HttpRequest req)
    {
        var qdrantCheck = await CheckQdrantAsync();
        var deepSeekCheck = CheckDeepSeek();
        var onnxCheck = CheckOnnxModel();

        // Roll up to "degraded" if any single module is down
        var allHealthy = qdrantCheck.Status == "healthy"
                      && deepSeekCheck.Status == "healthy"
                      && onnxCheck.Status == "healthy";

        var result = new
        {
            status = allHealthy ? "healthy" : "degraded",
            modules = new
            {
                qdrant = qdrantCheck,
                deepSeek = deepSeekCheck,
                onnxEmbedding = onnxCheck
            }
        };

        return new OkObjectResult(result);
    }

    // Uses ListCollections as a lightweight ping — if Qdrant is unreachable, the gRPC call throws
    private async Task<ComponentHealth> CheckQdrantAsync()
    {
        try
        {
            var collections = await _qdrantClient.ListCollectionsAsync();
            return new ComponentHealth("healthy", $"{collections.Count} collection(s)");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Qdrant health check failed");
            return new ComponentHealth("unhealthy", ex.Message);
        }
    }

    // Config-only check — doesn't call the DeepSeek API to avoid latency and rate-limit costs
    private ComponentHealth CheckDeepSeek()
    {
        var apiKey = _configuration["DeepSeek:ApiKey"];
        var endpoint = _configuration["DeepSeek:Endpoint"];
        var modelId = _configuration["DeepSeek:ModelId"];

        if (string.IsNullOrWhiteSpace(apiKey))
            return new ComponentHealth("unhealthy", "API key not configured");

        return new ComponentHealth("healthy", $"model={modelId ?? "deepseek-chat"}, endpoint={endpoint ?? "https://api.deepseek.com"}");
    }

    // Verifies the ONNX model files exist on disk (all-MiniLM-L6-v2, ~86MB + vocab)
    private ComponentHealth CheckOnnxModel()
    {
        // BaseDirectory points to the build output where CopyToOutputDirectory places the files
        var modelPath = Path.Combine(AppContext.BaseDirectory, "Models", "onnx", "model.onnx");
        var vocabPath = Path.Combine(AppContext.BaseDirectory, "Models", "onnx", "vocab.txt");

        if (!File.Exists(modelPath))
            return new ComponentHealth("unhealthy", $"model.onnx not found at {modelPath}");

        if (!File.Exists(vocabPath))
            return new ComponentHealth("unhealthy", $"vocab.txt not found at {vocabPath}");

        var modelSize = new FileInfo(modelPath).Length;
        return new ComponentHealth("healthy", $"model.onnx={modelSize / (1024 * 1024)}MB");
    }

    private record ComponentHealth(string Status, string Details);
}
