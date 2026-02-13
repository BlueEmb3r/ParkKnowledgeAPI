using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.Onnx;
using ParkKnowledgeAPI.Mcp;
using ParkKnowledgeAPI.Orchestration;
using ParkKnowledgeAPI.Services;
using ParkKnowledgeAPI.Services.Interfaces;
using Qdrant.Client;

#pragma warning disable SKEXP0010 // OpenAI custom endpoint (DeepSeek)
#pragma warning disable SKEXP0070 // ONNX connector

var builder = FunctionsApplication.CreateBuilder(args);

builder.ConfigureFunctionsWebApplication();

builder.Services
    .AddApplicationInsightsTelemetryWorkerService()
    .ConfigureFunctionsApplicationInsights();

// Semantic Kernel: register DeepSeek via OpenAI-compatible connector
builder.Services.AddOpenAIChatCompletion(
    modelId: builder.Configuration["DeepSeek:ModelId"] ?? "deepseek-chat",
    apiKey: builder.Configuration["DeepSeek:ApiKey"] ?? "",
    endpoint: new Uri(builder.Configuration["DeepSeek:Endpoint"] ?? "https://api.deepseek.com"));

// ONNX embedding model (all-MiniLM-L6-v2, 384 dims)
var onnxModelPath = Path.Combine(AppContext.BaseDirectory, "Models", "onnx", "model.onnx");
var vocabPath = Path.Combine(AppContext.BaseDirectory, "Models", "onnx", "vocab.txt");
builder.Services.AddBertOnnxEmbeddingGenerator(onnxModelPath, vocabPath);

// Qdrant client — reads host and gRPC port from configuration
builder.Services.AddSingleton(_ => new QdrantClient(
    host: builder.Configuration["Qdrant:Host"] ?? "localhost",
    port: int.Parse(builder.Configuration["Qdrant:GrpcPort"] ?? "6334")));

// Vector store
builder.Services.AddSingleton<IVectorStoreService, QdrantVectorStoreService>();

// Kernel is transient — lightweight wrapper, avoids cross-request mutation
builder.Services.AddTransient<Kernel>(sp => new Kernel(sp));

// MCP server — singleton so the agent can inject it; hosted service so the
// in-process server+client start/stop with the application lifecycle
builder.Services.AddSingleton<McpParkServer>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<McpParkServer>());

// Orchestration
builder.Services.AddTransient<ParkAssistantAgent>();

builder.Build().Run();
