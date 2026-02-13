using ParkKnowledgeAPI.Models;

namespace ParkKnowledgeAPI.Services.Interfaces;

public interface IVectorStoreService
{
    Task CreateCollectionIfNotExistsAsync(CancellationToken cancellationToken = default);

    Task UpsertParksAsync(
        IEnumerable<(string ParkCode, string ParkName, string State, string Content, ReadOnlyMemory<float> Embedding)> parks,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ParkSearchResult>> SearchAsync(
        ReadOnlyMemory<float> queryEmbedding,
        int limit = 5,
        CancellationToken cancellationToken = default);
}
