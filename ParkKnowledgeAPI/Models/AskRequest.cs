using System.Text.Json.Serialization;

namespace ParkKnowledgeAPI.Models;

public class AskRequest
{
    [JsonPropertyName("question")]
    public string Question { get; set; } = string.Empty;
}
