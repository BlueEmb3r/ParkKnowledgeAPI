using System.Text.Json.Serialization;

namespace ParkKnowledgeAPI.Models;

public class IngestResponse(string message, int count)
{
    [JsonPropertyName("message")]
    public string Message { get; } = message;

    [JsonPropertyName("count")]
    public int Count { get; } = count;
}
