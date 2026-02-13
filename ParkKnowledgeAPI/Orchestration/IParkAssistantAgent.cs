namespace ParkKnowledgeAPI.Orchestration;

public interface IParkAssistantAgent
{
    Task<string> AskAsync(string question, CancellationToken cancellationToken = default);
    IAsyncEnumerable<string> AskStreamingAsync(string question, CancellationToken cancellationToken = default);
}
