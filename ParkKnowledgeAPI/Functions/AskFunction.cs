using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using ParkKnowledgeAPI.Models;
using ParkKnowledgeAPI.Orchestration;

namespace ParkKnowledgeAPI.Functions;

public class AskFunction
{
    private readonly ParkAssistantAgent _agent;
    private readonly ILogger<AskFunction> _logger;

    public AskFunction(ParkAssistantAgent agent, ILogger<AskFunction> logger)
    {
        _agent = agent;
        _logger = logger;
    }

    [Function("Ask")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "ask")] HttpRequest req)
    {
        var body = await req.ReadFromJsonAsync<AskRequest>();

        if (body is null || string.IsNullOrWhiteSpace(body.Question))
        {
            _logger.LogWarning("Received ask request with empty question");
            return new BadRequestObjectResult(new { error = "Question is required." });
        }

        _logger.LogInformation("Ask endpoint called with question: {Question}", body.Question);

        var answer = await _agent.AskAsync(body.Question);

        return new OkObjectResult(new AskResponse { Answer = answer });
    }
}
