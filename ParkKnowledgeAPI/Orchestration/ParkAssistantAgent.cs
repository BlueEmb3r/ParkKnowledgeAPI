using System.Runtime.CompilerServices;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using ParkKnowledgeAPI.Mcp;

#pragma warning disable SKEXP0001 // AsKernelFunction() on AIFunction
#pragma warning disable SKEXP0010 // OpenAI custom endpoint (DeepSeek)
#pragma warning disable SKEXP0110 // ChatCompletionAgent

namespace ParkKnowledgeAPI.Orchestration;

public class ParkAssistantAgent
{
    private readonly Kernel _kernel;
    private readonly McpParkServer _mcpParkServer;
    private readonly ILogger<ParkAssistantAgent> _logger;

    private const string Instructions = """
        You are a knowledgeable national park assistant.
        Answer questions about US national parks accurately and concisely.

        IMPORTANT: Always use the search_parks tool to find relevant park information
        before answering questions. Base your answers on the search results.
        If the search returns no relevant results, say so honestly.
        """;

    public ParkAssistantAgent(Kernel kernel, McpParkServer mcpParkServer, ILogger<ParkAssistantAgent> logger)
    {
        _kernel = kernel;
        _mcpParkServer = mcpParkServer;
        _logger = logger;
    }

    public async Task<string> AskAsync(string question, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Agent processing question: {Question}", question);

        var agent = BuildAgent();
        var response = new StringBuilder();

        await foreach (var message in agent.InvokeAsync(
            new ChatMessageContent(AuthorRole.User, question), cancellationToken: cancellationToken))
        {
            response.Append(message.Message.Content);
        }

        _logger.LogInformation("Agent completed response");
        return response.ToString();
    }

    public async IAsyncEnumerable<string> AskStreamingAsync(
        string question,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Agent processing question (streaming): {Question}", question);

        var agent = BuildAgent();

        await foreach (StreamingChatMessageContent chunk in agent.InvokeStreamingAsync(
            new ChatMessageContent(AuthorRole.User, question),
            cancellationToken: cancellationToken))
        {
            if (!string.IsNullOrEmpty(chunk.Content))
                yield return chunk.Content;
        }

        _logger.LogInformation("Agent completed streaming response");
    }

    /// <summary>
    /// Creates a configured ChatCompletionAgent with a cloned kernel, MCP tools, and logging filter.
    /// Cloning prevents cross-request plugin pollution.
    /// </summary>
    private ChatCompletionAgent BuildAgent()
    {
        var kernel = _kernel.Clone();
        if (_mcpParkServer.Tools.Count > 0)
        {
            kernel.Plugins.AddFromFunctions("ParkTools",
                _mcpParkServer.Tools.Select(t => t.AsKernelFunction()));
            _logger.LogInformation("Imported {Count} MCP tools into kernel", _mcpParkServer.Tools.Count);
        }

        kernel.FunctionInvocationFilters.Add(new ToolInvocationLogger(_logger));

        return new ChatCompletionAgent
        {
            Name = "ParkAssistant",
            Instructions = Instructions,
            Kernel = kernel,
            Arguments = new KernelArguments(new OpenAIPromptExecutionSettings
            {
                Temperature = 0.0,
                FunctionChoiceBehavior = FunctionChoiceBehavior.Required()
            })
        };
    }

    /// <summary>Logs MCP tool calls and results in the request context where ILogger output is visible.</summary>
    private sealed class ToolInvocationLogger(ILogger logger) : IFunctionInvocationFilter
    {
        public async Task OnFunctionInvocationAsync(FunctionInvocationContext context, Func<FunctionInvocationContext, Task> next)
        {
            var args = string.Join(", ", context.Arguments.Select(a => $"{a.Key}={a.Value}"));
            logger.LogInformation("MCP tool {Tool} called with: {Args}", context.Function.Name, args);

            await next(context);

            var result = context.Result.ToString() ?? "";
            var lines = result.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            // Each result block starts with "## ParkName ..." — count and extract those headers
            var headers = lines.Where(l => l.StartsWith("## ")).ToList();
            var count = headers.Count;

            logger.LogInformation("MCP tool {Tool} returned {Count} results:", context.Function.Name, count);
            foreach (var header in headers)
            {
                logger.LogInformation("  {Header}", header);
            }
        }
    }
}
