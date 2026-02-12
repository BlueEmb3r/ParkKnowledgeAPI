using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.SemanticKernel;
using ParkKnowledgeAPI.Orchestration;

#pragma warning disable SKEXP0010 // OpenAI custom endpoint (DeepSeek)

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

// Kernel is transient — lightweight wrapper, avoids cross-request mutation
builder.Services.AddTransient<Kernel>(sp => new Kernel(sp));

// Orchestration
builder.Services.AddTransient<ParkAssistantAgent>();

builder.Build().Run();
