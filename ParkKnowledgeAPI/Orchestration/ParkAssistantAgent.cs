using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;

#pragma warning disable SKEXP0010
#pragma warning disable SKEXP0110

namespace ParkKnowledgeAPI.Orchestration;

public class ParkAssistantAgent
{
    private readonly Kernel _kernel;
    private readonly ILogger<ParkAssistantAgent> _logger;

    private const string Instructions = """
        You are a knowledgeable national park assistant.
        Answer questions about US national parks accurately and concisely.
        If you don't know the answer, say so honestly.
        """;

    public ParkAssistantAgent(Kernel kernel, ILogger<ParkAssistantAgent> logger)
    {
        _kernel = kernel;
        _logger = logger;
    }

    public async Task<string> AskAsync(string question)
    {
        _logger.LogInformation("Agent processing question: {Question}", question);

        ChatCompletionAgent agent = new()
        {
            Name = "ParkAssistant",
            Instructions = Instructions,
            Kernel = _kernel,
            Arguments = new KernelArguments(new OpenAIPromptExecutionSettings
            {
                Temperature = 0.0,
            })
        };

        var response = string.Empty;

        await foreach (var message in agent.InvokeAsync(
            new ChatMessageContent(AuthorRole.User, question)))
        {
            response += message.Message.Content;
        }

        _logger.LogInformation("Agent completed response");
        return response;
    }
}
