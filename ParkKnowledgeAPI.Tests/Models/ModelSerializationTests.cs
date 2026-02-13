using System.Text.Json;
using ParkKnowledgeAPI.Models;

namespace ParkKnowledgeAPI.Tests.Models;

public class ModelSerializationTests
{
    [Fact]
    public void AskRequest_RoundTrip_UsesCorrectJsonPropertyNames()
    {
        var original = new AskRequest { Question = "What parks are in Maine?" };
        var json = JsonSerializer.Serialize(original);
        Assert.Contains("\"question\"", json);
        Assert.DoesNotContain("\"Question\"", json);

        var deserialized = JsonSerializer.Deserialize<AskRequest>(json)!;
        Assert.Equal(original.Question, deserialized.Question);
    }

    [Fact]
    public void AskResponse_RoundTrip_UsesCorrectJsonPropertyNames()
    {
        var original = new AskResponse { Answer = "Acadia is in Maine." };
        var json = JsonSerializer.Serialize(original);
        Assert.Contains("\"answer\"", json);
        Assert.DoesNotContain("\"Answer\"", json);

        var deserialized = JsonSerializer.Deserialize<AskResponse>(json)!;
        Assert.Equal(original.Answer, deserialized.Answer);
    }

    [Fact]
    public void ErrorResponse_Serialization_UsesCorrectJsonPropertyNames()
    {
        var error = new ErrorResponse("Something went wrong.");
        var json = JsonSerializer.Serialize(error);
        Assert.Contains("\"error\"", json);
        Assert.DoesNotContain("\"Error\"", json);
        Assert.Contains("Something went wrong.", json);
    }

    [Fact]
    public void IngestRequest_RoundTrip_WithDocuments()
    {
        var original = new IngestRequest
        {
            Documents =
            [
                new DocumentInput { FileName = "acad.txt", Content = "Acadia National Park\nState(s): ME" },
                new DocumentInput { FileName = "yell.txt", Content = "Yellowstone National Park\nState(s): WY" }
            ]
        };

        var json = JsonSerializer.Serialize(original);
        Assert.Contains("\"documents\"", json);
        Assert.Contains("\"fileName\"", json);
        Assert.Contains("\"content\"", json);

        var deserialized = JsonSerializer.Deserialize<IngestRequest>(json)!;
        Assert.NotNull(deserialized.Documents);
        Assert.Equal(2, deserialized.Documents.Count);
        Assert.Equal("acad.txt", deserialized.Documents[0].FileName);
        Assert.Equal("Yellowstone National Park\nState(s): WY", deserialized.Documents[1].Content);
    }
}
