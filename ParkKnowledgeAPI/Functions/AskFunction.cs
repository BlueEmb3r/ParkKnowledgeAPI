using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using ParkKnowledgeAPI.Models;
using ParkKnowledgeAPI.Orchestration;

namespace ParkKnowledgeAPI.Functions;

public class AskFunction
{
    private readonly IParkAssistantAgent _agent;
    private readonly ILogger<AskFunction> _logger;

    public AskFunction(IParkAssistantAgent agent, ILogger<AskFunction> logger)
    {
        _agent = agent;
        _logger = logger;
    }

    [Function("Ask")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "ask")] HttpRequest req,
        CancellationToken cancellationToken)
    {
        var body = await req.ReadFromJsonAsync<AskRequest>(cancellationToken);

        if (body is null || string.IsNullOrWhiteSpace(body.Question))
        {
            _logger.LogWarning("Received ask request with empty question");
            return new BadRequestObjectResult(new ErrorResponse("Question is required."));
        }

        _logger.LogInformation("Ask endpoint called with question: {Question}", body.Question);

        try
        {
            var answer = await _agent.AskAsync(body.Question, cancellationToken);

            return new OkObjectResult(new AskResponse { Answer = answer });
        }
        catch (OperationCanceledException)
        {
            throw; // Let the runtime handle client disconnects
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process question: {Question}", body.Question);
            return new ObjectResult(new ErrorResponse("An error occurred while processing your question."))
            {
                StatusCode = StatusCodes.Status500InternalServerError
            };
        }
    }
}
