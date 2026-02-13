namespace ParkKnowledgeAPI.Models;

public record ParkSearchResult(
    string ParkCode,
    string ParkName,
    string State,
    string Content,
    float Score);
