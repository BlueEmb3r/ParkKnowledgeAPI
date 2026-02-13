using Microsoft.Extensions.AI;
using Moq;
using ParkKnowledgeAPI.Mcp;
using ParkKnowledgeAPI.Models;
using ParkKnowledgeAPI.Services.Interfaces;

namespace ParkKnowledgeAPI.Tests.Mcp;

public class ParkToolsTests
{
    private readonly Mock<IVectorStoreService> _vectorStoreMock = new();
    private readonly Mock<IEmbeddingGenerator<string, Embedding<float>>> _embeddingGenMock = new();

    private void SetupEmbeddingGenerator(float[] vector)
    {
        var embedding = new GeneratedEmbeddings<Embedding<float>>([new Embedding<float>(vector)]);
        _embeddingGenMock
            .Setup(e => e.GenerateAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<EmbeddingGenerationOptions?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(embedding);
    }

    [Fact]
    public async Task NoResults_ReturnsNoInfoMessage()
    {
        SetupEmbeddingGenerator([0.1f, 0.2f]);
        _vectorStoreMock
            .Setup(v => v.SearchAsync(It.IsAny<ReadOnlyMemory<float>>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ParkSearchResult>());

        var result = await ParkTools.SearchParksAsync(
            _vectorStoreMock.Object, _embeddingGenMock.Object, "test query", CancellationToken.None);

        Assert.Equal("No park information found for that query.", result);
    }

    [Fact]
    public async Task WithResults_ReturnsFormattedMarkdown()
    {
        SetupEmbeddingGenerator([0.1f, 0.2f]);
        _vectorStoreMock
            .Setup(v => v.SearchAsync(It.IsAny<ReadOnlyMemory<float>>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ParkSearchResult>
            {
                new("acad", "Acadia National Park", "ME", "Acadia content here.", 0.95f)
            });

        var result = await ParkTools.SearchParksAsync(
            _vectorStoreMock.Object, _embeddingGenMock.Object, "acadia", CancellationToken.None);

        Assert.Contains("## Acadia National Park (acad) — ME", result);
    }

    [Fact]
    public async Task WithResults_ContainsAllParkContent()
    {
        SetupEmbeddingGenerator([0.1f, 0.2f]);
        _vectorStoreMock
            .Setup(v => v.SearchAsync(It.IsAny<ReadOnlyMemory<float>>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ParkSearchResult>
            {
                new("acad", "Acadia National Park", "ME", "Acadia content here.", 0.95f),
                new("yell", "Yellowstone National Park", "WY", "Yellowstone content.", 0.88f)
            });

        var result = await ParkTools.SearchParksAsync(
            _vectorStoreMock.Object, _embeddingGenMock.Object, "parks", CancellationToken.None);

        Assert.Contains("Acadia content here.", result);
        Assert.Contains("Yellowstone content.", result);
    }

    [Fact]
    public async Task CallsEmbeddingGeneratorWithQuery()
    {
        SetupEmbeddingGenerator([0.1f, 0.2f]);
        _vectorStoreMock
            .Setup(v => v.SearchAsync(It.IsAny<ReadOnlyMemory<float>>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ParkSearchResult>());

        await ParkTools.SearchParksAsync(
            _vectorStoreMock.Object, _embeddingGenMock.Object, "mountains in colorado", CancellationToken.None);

        _embeddingGenMock.Verify(e => e.GenerateAsync(
            It.Is<IEnumerable<string>>(s => s.First() == "mountains in colorado"),
            It.IsAny<EmbeddingGenerationOptions?>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ScoreFormattedToThreeDecimals()
    {
        SetupEmbeddingGenerator([0.1f, 0.2f]);
        _vectorStoreMock
            .Setup(v => v.SearchAsync(It.IsAny<ReadOnlyMemory<float>>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ParkSearchResult>
            {
                new("acad", "Acadia", "ME", "Content.", 0.123456f)
            });

        var result = await ParkTools.SearchParksAsync(
            _vectorStoreMock.Object, _embeddingGenMock.Object, "test", CancellationToken.None);

        Assert.Contains("[score: 0.123]", result);
    }
}
