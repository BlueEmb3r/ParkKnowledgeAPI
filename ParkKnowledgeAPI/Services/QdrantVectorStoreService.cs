using Microsoft.Extensions.Logging;
using ParkKnowledgeAPI.Models;
using ParkKnowledgeAPI.Services.Interfaces;
using Qdrant.Client;
using Qdrant.Client.Grpc;

namespace ParkKnowledgeAPI.Services;

public class QdrantVectorStoreService : IVectorStoreService
{
    private const string CollectionName = "parks";
    private const int VectorSize = 384;

    private readonly QdrantClient _client;
    private readonly ILogger<QdrantVectorStoreService> _logger;

    public QdrantVectorStoreService(QdrantClient client, ILogger<QdrantVectorStoreService> logger)
    {
        _client = client;
        _logger = logger;
    }

    public async Task CreateCollectionIfNotExistsAsync(CancellationToken cancellationToken = default)
    {
        var collections = await _client.ListCollectionsAsync(cancellationToken);

        if (collections.Any(c => c == CollectionName))
        {
            _logger.LogInformation("Collection '{Collection}' already exists", CollectionName);
            return;
        }

        await _client.CreateCollectionAsync(
            CollectionName,
            new VectorParams { Size = VectorSize, Distance = Distance.Cosine },
            cancellationToken: cancellationToken);

        _logger.LogInformation("Created collection '{Collection}' with {Size} dimensions", CollectionName, VectorSize);
    }

    public async Task UpsertParksAsync(
        IEnumerable<(string ParkCode, string ParkName, string State, string Content, ReadOnlyMemory<float> Embedding)> parks,
        CancellationToken cancellationToken = default)
    {
        var points = parks.Select(park => new PointStruct
        {
            Id = new PointId { Uuid = GenerateDeterministicGuid(park.ParkCode).ToString() },
            Vectors = park.Embedding.ToArray(),
            Payload =
            {
                ["park_code"] = park.ParkCode,
                ["park_name"] = park.ParkName,
                ["state"] = park.State,
                ["content"] = park.Content
            }
        }).ToList();

        await _client.UpsertAsync(CollectionName, points, cancellationToken: cancellationToken);

        _logger.LogInformation("Upserted {Count} points into '{Collection}'", points.Count, CollectionName);
    }

    /// <summary>Cosine similarity search against the parks collection in Qdrant.</summary>
    public async Task<IReadOnlyList<ParkSearchResult>> SearchAsync(
        ReadOnlyMemory<float> queryEmbedding,
        int limit = 5,
        CancellationToken cancellationToken = default)
    {
        var results = await _client.SearchAsync(
            CollectionName,
            queryEmbedding.ToArray(),
            limit: (ulong)limit,
            cancellationToken: cancellationToken);

        // Map Qdrant ScoredPoint payload fields back to our domain model
        return results.Select(point => new ParkSearchResult(
            ParkCode: point.Payload.TryGetValue("park_code", out var code) ? code.StringValue : "",
            ParkName: point.Payload.TryGetValue("park_name", out var name) ? name.StringValue : "",
            State: point.Payload.TryGetValue("state", out var state) ? state.StringValue : "",
            Content: point.Payload.TryGetValue("content", out var content) ? content.StringValue : "",
            Score: point.Score
        )).ToList();
    }

    private static Guid GenerateDeterministicGuid(string input)
    {
        var bytes = System.Security.Cryptography.MD5.HashData(System.Text.Encoding.UTF8.GetBytes(input));
        return new Guid(bytes);
    }
}
