using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;
using ParkKnowledgeAPI.Functions;
using ParkKnowledgeAPI.Models;
using ParkKnowledgeAPI.Orchestration;
using ParkKnowledgeAPI.Tests.Helpers;

namespace ParkKnowledgeAPI.Tests.Functions;

public class AskStreamFunctionTests
{
    private readonly Mock<IParkAssistantAgent> _agentMock = new();
    private readonly Mock<ILogger<AskStreamFunction>> _loggerMock = new();
    private readonly AskStreamFunction _sut;

    public AskStreamFunctionTests()
    {
        _sut = new AskStreamFunction(_agentMock.Object, _loggerMock.Object);
    }

    private static string ReadResponseBody(HttpRequest req)
    {
        req.HttpContext.Response.Body.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(req.HttpContext.Response.Body, Encoding.UTF8);
        return reader.ReadToEnd();
    }

#pragma warning disable CS1998 // Async method lacks 'await' operators
    private static async IAsyncEnumerable<string> CreateAsyncEnumerable(params string[] items)
    {
        foreach (var item in items)
            yield return item;
    }
#pragma warning restore CS1998

    [Fact]
    public async Task Run_NullBody_Returns400WithErrorJson()
    {
        var req = HttpRequestHelper.CreateEmptyRequest();

        await _sut.Run(req, CancellationToken.None);

        Assert.Equal(StatusCodes.Status400BadRequest, req.HttpContext.Response.StatusCode);
        var body = ReadResponseBody(req);
        Assert.Contains("Question is required.", body);
    }

    [Fact]
    public async Task Run_EmptyQuestion_Returns400WithErrorJson()
    {
        var req = HttpRequestHelper.CreateJsonRequest(new AskRequest { Question = "" });

        await _sut.Run(req, CancellationToken.None);

        Assert.Equal(StatusCodes.Status400BadRequest, req.HttpContext.Response.StatusCode);
    }

    [Fact]
    public async Task Run_ValidQuestion_SetsCorrectSseHeaders()
    {
        _agentMock
            .Setup(a => a.AskStreamingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(CreateAsyncEnumerable("Hello"));

        var req = HttpRequestHelper.CreateJsonRequest(new AskRequest { Question = "Tell me about parks" });

        await _sut.Run(req, CancellationToken.None);

        Assert.Equal(StatusCodes.Status200OK, req.HttpContext.Response.StatusCode);
        Assert.Equal("text/event-stream; charset=utf-8", req.HttpContext.Response.ContentType);
        Assert.Equal("no-cache", req.HttpContext.Response.Headers.CacheControl.ToString());
        Assert.Equal("keep-alive", req.HttpContext.Response.Headers.Connection.ToString());
    }

    [Fact]
    public async Task Run_ValidQuestion_StreamsChunksAsSseEvents()
    {
        _agentMock
            .Setup(a => a.AskStreamingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(CreateAsyncEnumerable("Hello", " World"));

        var req = HttpRequestHelper.CreateJsonRequest(new AskRequest { Question = "Test" });

        await _sut.Run(req, CancellationToken.None);

        var body = ReadResponseBody(req);
        Assert.Contains("data: {\"content\":\"Hello\"}\n\n", body);
        Assert.Contains("data: {\"content\":\" World\"}\n\n", body);
    }

    [Fact]
    public async Task Run_ValidQuestion_SendsDoneMarker()
    {
        _agentMock
            .Setup(a => a.AskStreamingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(CreateAsyncEnumerable("chunk"));

        var req = HttpRequestHelper.CreateJsonRequest(new AskRequest { Question = "Test" });

        await _sut.Run(req, CancellationToken.None);

        var body = ReadResponseBody(req);
        Assert.EndsWith("data: [DONE]\n\n", body);
    }

    [Fact]
    public async Task Run_AgentThrowsException_WritesErrorSseEvent()
    {
        _agentMock
            .Setup(a => a.AskStreamingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(ThrowingAsyncEnumerable());

        var req = HttpRequestHelper.CreateJsonRequest(new AskRequest { Question = "Test" });

        await _sut.Run(req, CancellationToken.None);

        var body = ReadResponseBody(req);
        Assert.Contains("error", body);
        Assert.Contains("data: [DONE]\n\n", body);
    }

    [Fact]
    public async Task Run_AgentThrowsOperationCanceledException_Rethrows()
    {
        _agentMock
            .Setup(a => a.AskStreamingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(CancellingAsyncEnumerable());

        var req = HttpRequestHelper.CreateJsonRequest(new AskRequest { Question = "Test" });

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => _sut.Run(req, CancellationToken.None));
    }

#pragma warning disable CS1998
    private static async IAsyncEnumerable<string> ThrowingAsyncEnumerable()
    {
        throw new InvalidOperationException("LLM failure");
        yield break; // Required to make this an async enumerable
    }

    private static async IAsyncEnumerable<string> CancellingAsyncEnumerable()
    {
        throw new OperationCanceledException();
        yield break;
    }
#pragma warning restore CS1998
}
