using Bit.Core.Services;
using Bit.SharedWeb.Utilities;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using NSubstitute;

namespace SharedWeb.Test;

public class PlayIdMiddlewareTests
{
    private readonly PlayIdService _playIdService;
    private readonly RequestDelegate _next;
    private readonly PlayIdMiddleware _middleware;

    public PlayIdMiddlewareTests()
    {
        var hostEnvironment = Substitute.For<IHostEnvironment>();
        hostEnvironment.EnvironmentName.Returns(Environments.Development);

        _playIdService = new PlayIdService(hostEnvironment);
        _next = Substitute.For<RequestDelegate>();
        _middleware = new PlayIdMiddleware(_next);
    }

    [Fact]
    public async Task Invoke_WithValidPlayId_SetsPlayIdAndCallsNext()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers["x-play-id"] = "test-play-id";

        await _middleware.Invoke(context, _playIdService);

        Assert.Equal("test-play-id", _playIdService.PlayId);
        await _next.Received(1).Invoke(context);
    }

    [Fact]
    public async Task Invoke_WithoutPlayIdHeader_CallsNext()
    {
        var context = new DefaultHttpContext();

        await _middleware.Invoke(context, _playIdService);

        Assert.Null(_playIdService.PlayId);
        await _next.Received(1).Invoke(context);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t")]
    public async Task Invoke_WithEmptyOrWhitespacePlayId_Returns400(string playId)
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        context.Request.Headers["x-play-id"] = playId;

        await _middleware.Invoke(context, _playIdService);

        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
        await _next.DidNotReceive().Invoke(context);
    }

    [Fact]
    public async Task Invoke_WithPlayIdExceedingMaxLength_Returns400()
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        var longPlayId = new string('a', 257); // Exceeds 256 character limit
        context.Request.Headers["x-play-id"] = longPlayId;

        await _middleware.Invoke(context, _playIdService);

        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
        await _next.DidNotReceive().Invoke(context);
    }

    [Fact]
    public async Task Invoke_WithPlayIdAtMaxLength_SetsPlayIdAndCallsNext()
    {
        var context = new DefaultHttpContext();
        var maxLengthPlayId = new string('a', 256); // Exactly 256 characters
        context.Request.Headers["x-play-id"] = maxLengthPlayId;

        await _middleware.Invoke(context, _playIdService);

        Assert.Equal(maxLengthPlayId, _playIdService.PlayId);
        await _next.Received(1).Invoke(context);
    }

    [Fact]
    public async Task Invoke_WithSpecialCharactersInPlayId_SetsPlayIdAndCallsNext()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers["x-play-id"] = "test-play_id.123";

        await _middleware.Invoke(context, _playIdService);

        Assert.Equal("test-play_id.123", _playIdService.PlayId);
        await _next.Received(1).Invoke(context);
    }
}
