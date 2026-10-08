using Bit.AgentFill.Commands;
using Bit.AgentFill.Entities;
using Bit.AgentFill.Models;
using Bit.AgentFill.Queries;
using Bit.Core.Auth.Identity;
using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bitwarden.Server.Sdk.Features;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Xunit;

namespace Bit.AgentFill.Test;

public class AgentFillEndpointsTests
{
    private static readonly DateTimeOffset _now = new(2026, 10, 8, 12, 0, 0, TimeSpan.Zero);

    private readonly FakeTimeProvider _timeProvider = new(_now);
    private readonly ICurrentContext _currentContext = Substitute.For<ICurrentContext>();
    private readonly IDeviceRepository _deviceRepository = Substitute.For<IDeviceRepository>();
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _deviceId = Guid.NewGuid();

    public AgentFillEndpointsTests()
    {
        _currentContext.UserId.Returns(_userId);
        _currentContext.DeviceIdentifier.Returns("device-identifier");
        _deviceRepository.GetByIdentifierAsync("device-identifier", _userId)
            .Returns(new Device { Id = _deviceId, UserId = _userId, Identifier = "device-identifier" });
    }

    [Theory]
    [InlineData("POST", "/")]
    [InlineData("GET", "/{id:guid}")]
    [InlineData("PUT", "/{id:guid}")]
    public void MapAgentFillEndpoints_EveryEndpointRequiresApplicationPolicyAndTheFlag(string method, string route)
    {
        var app = WebApplication.CreateBuilder().Build();
        app.MapAgentFillEndpoints();

        var endpoint = ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(dataSource => dataSource.Endpoints)
            .OfType<RouteEndpoint>()
            .Single(e => e.RoutePattern.RawText == route &&
                         e.Metadata.GetMetadata<IHttpMethodMetadata>()!.HttpMethods.Contains(method));

        Assert.Equal(Policies.Application, endpoint.Metadata.GetMetadata<AuthorizeAttribute>()!.Policy);
        Assert.NotNull(endpoint.Metadata.GetMetadata<IFeatureMetadata>());
        Assert.Contains("AgentFill", endpoint.Metadata.GetMetadata<ITagsMetadata>()!.Tags);
        Assert.Equal("internal", endpoint.Metadata.GetMetadata<IEndpointGroupNameMetadata>()!.EndpointGroupName);
    }

    [Fact]
    public async Task CreateAsync_StoresTheRequestForTheCallingUserAndDevice()
    {
        var command = Substitute.For<ICreateApprovalRequestCommand>();
        var created = Request(sealedResponse: null);
        command.CreateAsync(_userId, _deviceId, "sealed-request").Returns(created);

        var result = await AgentFillEndpointsExtensions.CreateAsync(
            new CreateApprovalRequestRequest("sealed-request"), _currentContext, _deviceRepository, command,
            _timeProvider);

        Assert.Equal(created.Id, result.Value!.Id);
        Assert.Equal("pending", result.Value.Status);
    }

    [Fact]
    public async Task CreateAsync_UnknownDevice_IsBadRequest()
    {
        var command = Substitute.For<ICreateApprovalRequestCommand>();
        _currentContext.DeviceIdentifier.Returns("unknown-device");

        await Assert.ThrowsAsync<BadRequestException>(() => AgentFillEndpointsExtensions.CreateAsync(
            new CreateApprovalRequestRequest("sealed-request"), _currentContext, _deviceRepository, command,
            _timeProvider));

        await command.DidNotReceiveWithAnyArgs().CreateAsync(default, default, default!);
    }

    [Fact]
    public async Task CreateAsync_OversizedSealedRequest_IsBadRequest()
    {
        var command = Substitute.For<ICreateApprovalRequestCommand>();

        await Assert.ThrowsAsync<BadRequestException>(() => AgentFillEndpointsExtensions.CreateAsync(
            new CreateApprovalRequestRequest(new string('a', SealedField.MaxBytes + 1)), _currentContext,
            _deviceRepository, command, _timeProvider));

        await command.DidNotReceiveWithAnyArgs().CreateAsync(default, default, default!);
    }

    [Fact]
    public async Task GetAsync_OwnRecord_ReturnsIt()
    {
        var query = Substitute.For<IGetApprovalRequestQuery>();
        var record = Request(sealedResponse: null);
        query.GetAsync(record.Id, _userId).Returns(record);

        var result = await AgentFillEndpointsExtensions.GetAsync(record.Id, _currentContext, query, _timeProvider);

        var ok = Assert.IsType<Ok<ApprovalRecordResponseModel>>(result.Result);
        Assert.Equal(record.Id, ok.Value!.Id);
    }

    [Fact]
    public async Task GetAsync_AnotherUsersRecord_IsNotFound()
    {
        // The query is scoped by the caller's user ID, so another user's record reads as missing.
        var query = Substitute.For<IGetApprovalRequestQuery>();
        query.GetAsync(default, default).ReturnsForAnyArgs((AgentFillApprovalRequest?)null);

        var result = await AgentFillEndpointsExtensions.GetAsync(Guid.NewGuid(), _currentContext, query, _timeProvider);

        Assert.IsType<NotFound>(result.Result);
    }

    [Fact]
    public async Task AnswerAsync_Answered_IsOkWithTheRecord()
    {
        var record = Request(sealedResponse: "sealed-response");
        var result = await Answer(record.Id, new AnswerApprovalRequestResult(AnswerOutcome.Answered, record));

        var ok = Assert.IsType<Ok<ApprovalRecordResponseModel>>(result.Result);
        Assert.Equal("answered", ok.Value!.Status);
    }

    [Fact]
    public async Task AnswerAsync_AlreadyAnswered_IsConflictWithTheStoredRecord()
    {
        var record = Request(sealedResponse: "first-response");
        var result = await Answer(record.Id, new AnswerApprovalRequestResult(AnswerOutcome.AlreadyAnswered, record));

        var conflict = Assert.IsType<Conflict<ApprovalRecordResponseModel>>(result.Result);
        Assert.Equal("first-response", conflict.Value!.SealedResponse);
    }

    [Fact]
    public async Task AnswerAsync_Expired_IsGone()
    {
        var result = await Answer(Guid.NewGuid(), new AnswerApprovalRequestResult(AnswerOutcome.Expired, Request(null)));

        var status = Assert.IsType<StatusCodeHttpResult>(result.Result);
        Assert.Equal(StatusCodes.Status410Gone, status.StatusCode);
    }

    [Fact]
    public async Task AnswerAsync_NotFound_IsNotFound()
    {
        var result = await Answer(Guid.NewGuid(), new AnswerApprovalRequestResult(AnswerOutcome.NotFound, null));

        Assert.IsType<NotFound>(result.Result);
    }

    private async Task<Results<Ok<ApprovalRecordResponseModel>, NotFound, Conflict<ApprovalRecordResponseModel>, StatusCodeHttpResult>> Answer(
        Guid id, AnswerApprovalRequestResult commandResult)
    {
        var command = Substitute.For<IAnswerApprovalRequestCommand>();
        command.AnswerAsync(id, _userId, _deviceId, "sealed-response").Returns(commandResult);

        return await AgentFillEndpointsExtensions.AnswerAsync(id, new AnswerApprovalRequestRequest("sealed-response"),
            _currentContext, _deviceRepository, command, _timeProvider);
    }

    private AgentFillApprovalRequest Request(string? sealedResponse) => new()
    {
        Id = Guid.NewGuid(),
        UserId = _userId,
        RequestDeviceId = _deviceId,
        SealedRequest = "sealed-request",
        SealedResponse = sealedResponse,
        CreationDate = _now.UtcDateTime.AddMinutes(-1),
        ExpirationDate = _now.UtcDateTime.AddMinutes(4),
    };
}
