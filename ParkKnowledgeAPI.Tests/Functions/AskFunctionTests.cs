using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using ParkKnowledgeAPI.Functions;
using ParkKnowledgeAPI.Models;
using ParkKnowledgeAPI.Orchestration;
using ParkKnowledgeAPI.Tests.Helpers;

namespace ParkKnowledgeAPI.Tests.Functions;

public class AskFunctionTests
{
    private readonly Mock<IParkAssistantAgent> _agentMock = new();
    private readonly Mock<ILogger<AskFunction>> _loggerMock = new();
    private readonly AskFunction _sut;

    public AskFunctionTests()
    {
        _sut = new AskFunction(_agentMock.Object, _loggerMock.Object);
    }

    [Fact]
    public async Task Run_NullBody_ReturnsBadRequest()
    {
        var req = HttpRequestHelper.CreateEmptyRequest();

        var result = await _sut.Run(req, CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        var error = Assert.IsType<ErrorResponse>(badRequest.Value);
        Assert.Equal("Question is required.", error.Error);
    }

    [Fact]
    public async Task Run_EmptyQuestion_ReturnsBadRequest()
    {
        var req = HttpRequestHelper.CreateJsonRequest(new AskRequest { Question = "" });

        var result = await _sut.Run(req, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task Run_WhitespaceQuestion_ReturnsBadRequest()
    {
        var req = HttpRequestHelper.CreateJsonRequest(new AskRequest { Question = "   " });

        var result = await _sut.Run(req, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task Run_ValidQuestion_ReturnsOkWithAnswer()
    {
        _agentMock
            .Setup(a => a.AskAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("Acadia is in Maine.");

        var req = HttpRequestHelper.CreateJsonRequest(new AskRequest { Question = "Where is Acadia?" });

        var result = await _sut.Run(req, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<AskResponse>(okResult.Value);
        Assert.Equal("Acadia is in Maine.", response.Answer);
    }

    [Fact]
    public async Task Run_AgentThrowsException_Returns500()
    {
        _agentMock
            .Setup(a => a.AskAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("LLM failure"));

        var req = HttpRequestHelper.CreateJsonRequest(new AskRequest { Question = "Tell me about parks" });

        var result = await _sut.Run(req, CancellationToken.None);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(500, objectResult.StatusCode);
    }

    [Fact]
    public async Task Run_AgentThrowsOperationCanceledException_Rethrows()
    {
        _agentMock
            .Setup(a => a.AskAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        var req = HttpRequestHelper.CreateJsonRequest(new AskRequest { Question = "Test question" });

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => _sut.Run(req, CancellationToken.None));
    }
}
