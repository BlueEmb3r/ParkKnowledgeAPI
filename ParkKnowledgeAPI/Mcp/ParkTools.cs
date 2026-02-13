using System.Text;
using Microsoft.Extensions.AI;
using ParkKnowledgeAPI.Services.Interfaces;

namespace ParkKnowledgeAPI.Mcp;

/// <summary>
/// MCP tool implementations. Called by the in-process MCP server when the LLM invokes a tool.
/// </summary>
public static class ParkTools
{
    /// <summary>Embeds the query, runs cosine similarity search in Qdrant, and formats results for the LLM.</summary>
    public static async Task<string> SearchParksAsync(
        IVectorStoreService vectorStore,
        IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator,
        string query,
        CancellationToken cancellationToken)
    {
        var embeddings = await embeddingGenerator.GenerateAsync([query], cancellationToken: cancellationToken);
        var results = await vectorStore.SearchAsync(embeddings[0].Vector, limit: 5, cancellationToken: cancellationToken);

        if (results.Count == 0)
            return "No park information found for that query.";

        var sb = new StringBuilder();
        foreach (var r in results)
        {
            sb.AppendLine($"## {r.ParkName} ({r.ParkCode}) — {r.State}  [score: {r.Score:F3}]");
            sb.AppendLine(r.Content);
            sb.AppendLine();
        }

        return sb.ToString();
    }
}
