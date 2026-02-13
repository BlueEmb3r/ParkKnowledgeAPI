using System.Text.Json.Serialization;

namespace ParkKnowledgeAPI.Models;

public class IngestRequest
{
    [JsonPropertyName("documents")]
    public List<DocumentInput>? Documents { get; set; }
}

public class DocumentInput
{
    [JsonPropertyName("fileName")]
    public string FileName { get; set; } = string.Empty;

    [JsonPropertyName("content")]
    public string Content { get; set; } = string.Empty;
}
