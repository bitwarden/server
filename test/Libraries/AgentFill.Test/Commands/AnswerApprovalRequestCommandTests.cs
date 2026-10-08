using Bit.AgentFill.Commands;
using Bit.AgentFill.Entities;
using Bit.AgentFill.Repositories;
using Bit.Core.Enums;
using Bit.Core.Platform.Push;
using Bit.Core.Platform.Push.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Xunit;

namespace Bit.AgentFill.Test.Commands;

public class AnswerApprovalRequestCommandTests
{
    private static readonly DateTimeOffset _now = new(2026, 10, 8, 12, 0, 0, TimeSpan.Zero);

    private readonly IAgentFillApprovalRequestRepository _repository = Substitute.For<IAgentFillApprovalRequestRepository>();
    private readonly IPushNotificationService _push = Substitute.For<IPushNotificationService>();
    private readonly AnswerApprovalRequestCommand _sut;

    private readonly Guid _id = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _deviceId = Guid.NewGuid();

    public AnswerApprovalRequestCommandTests()
    {
        _sut = new AnswerApprovalRequestCommand(_repository, _push, new FakeTimeProvider(_now),
            NullLogger<AnswerApprovalRequestCommand>.Instance);
    }

    [Fact]
    public async Task AnswerAsync_FirstAnswer_RecordsItAndPushesToOtherDesktops()
    {
        var stored = Request(sealedResponse: "sealed-response");
        _repository.AnswerAsync(_id, _userId, "sealed-response", _deviceId, _now.UtcDateTime).Returns(1);
        _repository.GetByIdAsync(_id, _userId).Returns(stored);

        var result = await _sut.AnswerAsync(_id, _userId, _deviceId, "sealed-response");

        Assert.Equal(AnswerOutcome.Answered, result.Outcome);
        Assert.Same(stored, result.Request);
        await _push.Received(1).PushAsync(Arg.Is<PushNotification<AgentFillApprovalPushNotification>>(n =>
            n.Type == PushType.AgentFillApprovalResponse &&
            n.Target == NotificationTarget.User &&
            n.TargetId == _userId &&
            n.ClientType == ClientType.Desktop &&
            n.ExcludeCurrentContext &&
            n.Payload.Id == _id &&
            n.Payload.UserId == _userId));
    }

    [Fact]
    public async Task AnswerAsync_AlreadyAnswered_ReturnsTheStoredRecordWithoutPushing()
    {
        var stored = Request(sealedResponse: "first-response");
        _repository.AnswerAsync(default, default, default!, default, default).ReturnsForAnyArgs(0);
        _repository.GetByIdAsync(_id, _userId).Returns(stored);

        var result = await _sut.AnswerAsync(_id, _userId, _deviceId, "second-response");

        Assert.Equal(AnswerOutcome.AlreadyAnswered, result.Outcome);
        Assert.Equal("first-response", result.Request!.SealedResponse);
        await _push.DidNotReceiveWithAnyArgs().PushAsync<AgentFillApprovalPushNotification>(default!);
    }

    [Fact]
    public async Task AnswerAsync_Expired_ReturnsExpiredWithoutPushing()
    {
        var stored = Request(sealedResponse: null, expirationDate: _now.UtcDateTime.AddSeconds(-1));
        _repository.AnswerAsync(default, default, default!, default, default).ReturnsForAnyArgs(0);
        _repository.GetByIdAsync(_id, _userId).Returns(stored);

        var result = await _sut.AnswerAsync(_id, _userId, _deviceId, "late-response");

        Assert.Equal(AnswerOutcome.Expired, result.Outcome);
        await _push.DidNotReceiveWithAnyArgs().PushAsync<AgentFillApprovalPushNotification>(default!);
    }

    [Fact]
    public async Task AnswerAsync_AnotherUsersRecord_ReturnsNotFound()
    {
        // The repository scopes by owner, so another user's record reads as missing.
        _repository.AnswerAsync(default, default, default!, default, default).ReturnsForAnyArgs(0);
        _repository.GetByIdAsync(_id, _userId).Returns((AgentFillApprovalRequest?)null);

        var result = await _sut.AnswerAsync(_id, _userId, _deviceId, "sealed-response");

        Assert.Equal(AnswerOutcome.NotFound, result.Outcome);
        Assert.Null(result.Request);
        await _push.DidNotReceiveWithAnyArgs().PushAsync<AgentFillApprovalPushNotification>(default!);
    }

    [Fact]
    public async Task AnswerAsync_UsesTheCallerAsOwnerAndResponder()
    {
        _repository.GetByIdAsync(_id, _userId).Returns(Request(sealedResponse: "sealed-response"));
        _repository.AnswerAsync(default, default, default!, default, default).ReturnsForAnyArgs(1);

        await _sut.AnswerAsync(_id, _userId, _deviceId, "sealed-response");

        await _repository.Received(1).AnswerAsync(_id, _userId, "sealed-response", _deviceId, _now.UtcDateTime);
    }

    private AgentFillApprovalRequest Request(string? sealedResponse, DateTime? expirationDate = null) => new()
    {
        Id = _id,
        UserId = _userId,
        RequestDeviceId = Guid.NewGuid(),
        SealedRequest = "sealed-request",
        SealedResponse = sealedResponse,
        CreationDate = _now.UtcDateTime.AddMinutes(-1),
        ExpirationDate = expirationDate ?? _now.UtcDateTime.AddMinutes(4),
    };
}
