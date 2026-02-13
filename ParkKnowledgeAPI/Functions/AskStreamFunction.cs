using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using ParkKnowledgeAPI.Models;
using ParkKnowledgeAPI.Orchestration;

namespace ParkKnowledgeAPI.Functions;

public class AskStreamFunction
{
    private readonly ParkAssistantAgent _agent;
    private readonly ILogger<AskStreamFunction> _logger;

    public AskStreamFunction(ParkAssistantAgent agent, ILogger<AskStreamFunction> logger)
    {
        _agent = agent;
        _logger = logger;
    }

    [Function("AskStream")]
    public async Task Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "ask/stream")] HttpRequest req,
        CancellationToken cancellationToken)
    {
        var body = await req.ReadFromJsonAsync<AskRequest>(cancellationToken);

        if (body is null || string.IsNullOrWhiteSpace(body.Question))
        {
            req.HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
            await req.HttpContext.Response.WriteAsJsonAsync(
                new ErrorResponse("Question is required."), cancellationToken);
            return;
        }

        _logger.LogInformation("AskStream endpoint called with question: {Question}", body.Question);

        var response = req.HttpContext.Response;
        response.StatusCode = StatusCodes.Status200OK;
        response.ContentType = "text/event-stream; charset=utf-8";
        response.Headers.CacheControl = "no-cache";
        response.Headers.Connection = "keep-alive";

        try
        {
            await foreach (var chunk in _agent.AskStreamingAsync(body.Question, cancellationToken))
            {
                var json = JsonSerializer.Serialize(new { content = chunk });
                await response.WriteAsync($"data: {json}\n\n", cancellationToken);
                await response.Body.FlushAsync(cancellationToken);
            }

            await response.WriteAsync("data: [DONE]\n\n", cancellationToken);
            await response.Body.FlushAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw; // Client disconnected
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Streaming failed for question: {Question}", body.Question);

            var errorJson = JsonSerializer.Serialize(new { error = "An error occurred while processing your question." });
            await response.WriteAsync($"data: {errorJson}\n\n", cancellationToken);
            await response.WriteAsync("data: [DONE]\n\n", cancellationToken);
            await response.Body.FlushAsync(cancellationToken);
        }
    }
}
