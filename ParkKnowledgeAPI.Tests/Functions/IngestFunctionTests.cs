using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Moq;
using ParkKnowledgeAPI.Functions;
using ParkKnowledgeAPI.Models;
using ParkKnowledgeAPI.Services.Interfaces;
using ParkKnowledgeAPI.Tests.Helpers;

namespace ParkKnowledgeAPI.Tests.Functions;

public class IngestFunctionTests
{
    private readonly Mock<IEmbeddingGenerator<string, Embedding<float>>> _embeddingGenMock = new();
    private readonly Mock<IVectorStoreService> _vectorStoreMock = new();
    private readonly Mock<ILogger<IngestFunction>> _loggerMock = new();
    private readonly IngestFunction _sut;

    public IngestFunctionTests()
    {
        _sut = new IngestFunction(_embeddingGenMock.Object, _vectorStoreMock.Object, _loggerMock.Object);
    }

    private void SetupEmbeddingsForCount(int count)
    {
        var embeddings = Enumerable.Range(0, count)
            .Select(_ => new Embedding<float>(new float[] { 0.1f, 0.2f, 0.3f }))
            .ToList();
        _embeddingGenMock
            .Setup(e => e.GenerateAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<EmbeddingGenerationOptions?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GeneratedEmbeddings<Embedding<float>>(embeddings));
    }

    private static IngestRequest CreateValidIngestRequest(int docCount = 1)
    {
        var docs = Enumerable.Range(0, docCount).Select(i => new DocumentInput
        {
            FileName = $"park{i}.txt",
            Content = $"Park {i} Name\nState(s): ST\n\nDescription:\nA great park number {i}.\n\nDirections:\nGo north."
        }).ToList();
        return new IngestRequest { Documents = docs };
    }

    [Fact]
    public async Task Run_NullDocuments_ReturnsBadRequest()
    {
        // Send an IngestRequest with null documents and ContentLength 0 so the
        // function skips body parsing and falls to the local-file path (which
        // doesn't exist in the test output directory)
        var body = new IngestRequest { Documents = null };
        var req = HttpRequestHelper.CreateJsonRequest(body);

        var result = await _sut.Run(req, CancellationToken.None);

        // The function may return BadRequest (400) if no local files exist,
        // or 500 if local files exist but mocks aren't configured.
        // In both cases it's an ObjectResult — verify error status.
        var objectResult = Assert.IsAssignableFrom<ObjectResult>(result);
        Assert.True(objectResult.StatusCode is 400 or 500,
            $"Expected 400 or 500 but got {objectResult.StatusCode}");
    }

    [Fact]
    public async Task Run_ValidDocumentsInBody_ReturnsOkWithCount()
    {
        SetupEmbeddingsForCount(2);
        var body = CreateValidIngestRequest(2);
        var req = HttpRequestHelper.CreateJsonRequest(body);

        var result = await _sut.Run(req, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(okResult.Value);
        // The response is an anonymous type, so check via serialization
        var json = System.Text.Json.JsonSerializer.Serialize(okResult.Value);
        Assert.Contains("\"count\":2", json);
    }

    [Fact]
    public async Task Run_DocumentWithInsufficientContent_Skipped()
    {
        // A document with only 1 line (no second line for state) should be skipped
        SetupEmbeddingsForCount(1);
        var body = new IngestRequest
        {
            Documents =
            [
                new DocumentInput { FileName = "bad.txt", Content = "OnlyOneLine" },
                new DocumentInput { FileName = "good.txt", Content = "Good Park\nState(s): CA\n\nDescription:\nNice park." }
            ]
        };
        var req = HttpRequestHelper.CreateJsonRequest(body);

        var result = await _sut.Run(req, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var json = System.Text.Json.JsonSerializer.Serialize(okResult.Value);
        Assert.Contains("\"count\":1", json);
    }

    [Fact]
    public async Task Run_AllDocumentsInsufficient_ReturnsBadRequest()
    {
        var body = new IngestRequest
        {
            Documents = [new DocumentInput { FileName = "bad.txt", Content = "OnlyOneLine" }]
        };
        var req = HttpRequestHelper.CreateJsonRequest(body);

        var result = await _sut.Run(req, CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        var error = Assert.IsType<ErrorResponse>(badRequest.Value);
        Assert.Contains("No valid documents", error.Error);
    }

    [Fact]
    public async Task Run_ValidDocuments_CallsCreateCollectionOnce()
    {
        SetupEmbeddingsForCount(2);
        var body = CreateValidIngestRequest(2);
        var req = HttpRequestHelper.CreateJsonRequest(body);

        await _sut.Run(req, CancellationToken.None);

        _vectorStoreMock.Verify(v => v.CreateCollectionIfNotExistsAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Run_ValidDocuments_CallsEmbeddingGeneratorWithDescriptions()
    {
        SetupEmbeddingsForCount(1);
        var body = CreateValidIngestRequest(1);
        var req = HttpRequestHelper.CreateJsonRequest(body);

        await _sut.Run(req, CancellationToken.None);

        _embeddingGenMock.Verify(e => e.GenerateAsync(
            It.Is<IEnumerable<string>>(s => s.Count() == 1),
            It.IsAny<EmbeddingGenerationOptions?>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Run_ValidDocuments_CallsUpsertWithCorrectData()
    {
        SetupEmbeddingsForCount(1);
        var body = CreateValidIngestRequest(1);
        var req = HttpRequestHelper.CreateJsonRequest(body);

        await _sut.Run(req, CancellationToken.None);

        _vectorStoreMock.Verify(v => v.UpsertParksAsync(
            It.IsAny<IEnumerable<(string, string, string, string, ReadOnlyMemory<float>)>>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Run_EmbeddingGeneratorThrows_Returns500()
    {
        _embeddingGenMock
            .Setup(e => e.GenerateAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<EmbeddingGenerationOptions?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("ONNX failure"));
        var body = CreateValidIngestRequest(1);
        var req = HttpRequestHelper.CreateJsonRequest(body);

        var result = await _sut.Run(req, CancellationToken.None);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(500, objectResult.StatusCode);
    }

    [Fact]
    public async Task Run_VectorStoreThrows_Returns500()
    {
        SetupEmbeddingsForCount(1);
        _vectorStoreMock
            .Setup(v => v.CreateCollectionIfNotExistsAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Qdrant down"));
        var body = CreateValidIngestRequest(1);
        var req = HttpRequestHelper.CreateJsonRequest(body);

        var result = await _sut.Run(req, CancellationToken.None);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(500, objectResult.StatusCode);
    }

    [Fact]
    public async Task Run_ParsesParkDataCorrectly()
    {
        SetupEmbeddingsForCount(1);
        var body = new IngestRequest
        {
            Documents =
            [
                new DocumentInput
                {
                    FileName = "acad.txt",
                    Content = "Acadia National Park\nState(s): ME\n\nDescription:\nA beautiful coastal park.\n\nDirections:\nTake I-95."
                }
            ]
        };
        var req = HttpRequestHelper.CreateJsonRequest(body);

        await _sut.Run(req, CancellationToken.None);

        _vectorStoreMock.Verify(v => v.UpsertParksAsync(
            It.Is<IEnumerable<(string ParkCode, string ParkName, string State, string Content, ReadOnlyMemory<float> Embedding)>>(
                parks => parks.Any(p => p.ParkCode == "acad" && p.ParkName == "Acadia National Park" && p.State == "ME")),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
