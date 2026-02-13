using System.Text.Json.Serialization;

namespace ParkKnowledgeAPI.Models;

public class ErrorResponse(string error)
{
    [JsonPropertyName("error")]
    public string Error { get; } = error;
}
