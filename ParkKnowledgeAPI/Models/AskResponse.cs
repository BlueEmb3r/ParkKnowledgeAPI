using System.Text.Json.Serialization;

namespace ParkKnowledgeAPI.Models;

public class AskResponse
{
    [JsonPropertyName("answer")]
    public string Answer { get; set; } = string.Empty;
}
