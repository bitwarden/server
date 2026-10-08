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

public class CreateApprovalRequestCommandTests
{
    private static readonly DateTimeOffset _now = new(2026, 10, 8, 12, 0, 0, TimeSpan.Zero);

    private readonly IAgentFillApprovalRequestRepository _repository = Substitute.For<IAgentFillApprovalRequestRepository>();
    private readonly IPushNotificationService _push = Substitute.For<IPushNotificationService>();
    private readonly CreateApprovalRequestCommand _sut;

    public CreateApprovalRequestCommandTests()
    {
        _sut = new CreateApprovalRequestCommand(_repository, _push, new FakeTimeProvider(_now),
            NullLogger<CreateApprovalRequestCommand>.Instance);
    }

    [Fact]
    public async Task CreateAsync_StoresTheRequestForFiveMinutes()
    {
        var userId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();

        var created = await _sut.CreateAsync(userId, deviceId, "sealed-request");

        Assert.NotEqual(Guid.Empty, created.Id);
        Assert.Equal(userId, created.UserId);
        Assert.Equal(deviceId, created.RequestDeviceId);
        Assert.Equal("sealed-request", created.SealedRequest);
        Assert.Null(created.SealedResponse);
        Assert.Equal(_now.UtcDateTime, created.CreationDate);
        Assert.Equal(_now.UtcDateTime.AddMinutes(5), created.ExpirationDate);
        await _repository.Received(1).CreateAsync(created);
    }

    [Fact]
    public async Task CreateAsync_PushesIdsOnlyToMobileClients()
    {
        var userId = Guid.NewGuid();

        var created = await _sut.CreateAsync(userId, Guid.NewGuid(), "sealed-request");

        await _push.Received(1).PushAsync(Arg.Is<PushNotification<AgentFillApprovalPushNotification>>(n =>
            n.Type == PushType.AgentFillApprovalRequest &&
            n.Target == NotificationTarget.User &&
            n.TargetId == userId &&
            n.ClientType == ClientType.Mobile &&
            !n.ExcludeCurrentContext &&
            n.Payload.Id == created.Id &&
            n.Payload.UserId == userId));
    }

    [Fact]
    public async Task CreateAsync_StoresBeforePushing()
    {
        await _sut.CreateAsync(Guid.NewGuid(), Guid.NewGuid(), "sealed-request");

        Received.InOrder(() =>
        {
            _repository.CreateAsync(Arg.Any<AgentFillApprovalRequest>());
            _push.PushAsync(Arg.Any<PushNotification<AgentFillApprovalPushNotification>>());
        });
    }
}
