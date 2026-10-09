using Bit.Core.Auth.Sso;
using Bit.Sso.Utilities.Saml2;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace Bit.SSO.Test.Utilities;

/// <summary>
/// Tests for <see cref="Saml2Rsa15DeprecationNotifier"/>.
/// While a request to claim the send interval is processing against a given organization,
/// <c>TryQueue</c> ignores more calls for that organization.
/// </summary>
public class Saml2Rsa15DeprecationNotifierTests
{
    private static readonly TimeSpan _wait = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan _negativeWait = TimeSpan.FromMilliseconds(250);

    private readonly ISaml2Rsa15DeprecationNoticeInterval _interval = Substitute.For<ISaml2Rsa15DeprecationNoticeInterval>();
    private readonly ISaml2Rsa15DeprecationNoticeCommand _command = Substitute.For<ISaml2Rsa15DeprecationNoticeCommand>();
    private readonly ILogger<Saml2Rsa15DeprecationNotifier> _logger = Substitute.For<ILogger<Saml2Rsa15DeprecationNotifier>>();
    private readonly Saml2Rsa15DeprecationNotifier _sut;

    public Saml2Rsa15DeprecationNotifierTests()
    {
        _sut = new Saml2Rsa15DeprecationNotifier(_interval, _command, _logger);
    }

    private static TaskCompletionSource NewSignal() => new(TaskCreationOptions.RunContinuationsAsynchronously);

    private static TaskCompletionSource<bool> NewClaimGate() => new(TaskCreationOptions.RunContinuationsAsynchronously);

    // A given organization is released from the guard  after the claim returns or throws, so a repeat call can be dropped
    // until then. Repeat the call until the claim count is reached.
    private async Task QueueUntilClaimCountAsync(Guid organizationId, int expected)
    {
        var deadline = DateTime.UtcNow + _wait;
        while (_interval.ReceivedCalls().Count() < expected)
        {
            if (DateTime.UtcNow > deadline)
            {
                throw new TimeoutException($"Expected {expected} claim call(s).");
            }
            _sut.TryQueue(organizationId);
            await Task.Delay(10);
        }
    }

    private async Task WaitForLogCountAsync(int expected)
    {
        var deadline = DateTime.UtcNow + _wait;
        while (_logger.ReceivedCalls().Count(call => call.GetMethodInfo().Name == nameof(ILogger.Log)) < expected)
        {
            if (DateTime.UtcNow > deadline)
            {
                throw new TimeoutException($"Expected {expected} log call(s).");
            }
            await Task.Delay(10);
        }
    }

    /// <summary>
    /// Arranges a claim that returns true. It proves that the notifier calls the send for the
    /// same organization on a background task. 
    /// </summary>
    [Fact]
    public async Task TryQueue_ClaimSucceeds_SendsNoticeInBackground()
    {
        var organizationId = Guid.NewGuid();
        var sent = NewSignal();
        _interval.TryClaimIntervalAsync(organizationId).Returns(true);
        _command.SendAsync(organizationId).Returns(_ =>
        {
            sent.TrySetResult();
            return Task.CompletedTask;
        });

        _sut.TryQueue(organizationId);

        await sent.Task.WaitAsync(_wait);
        await _command.Received(1).SendAsync(organizationId);
    }

    /// <summary>
    /// Arranges a claim that returns false. An organization receives no second
    /// notice in the same interval.
    /// </summary>
    [Fact]
    public async Task TryQueue_ClaimDenied_DoesNotSendNotice()
    {
        var organizationId = Guid.NewGuid();
        var claimed = NewSignal();
        _interval.TryClaimIntervalAsync(organizationId).Returns(_ =>
        {
            claimed.TrySetResult();
            return Task.FromResult(false);
        });

        _sut.TryQueue(organizationId);

        await claimed.Task.WaitAsync(_wait);
        await Task.Delay(_negativeWait);
        await _command.DidNotReceive().SendAsync(Arg.Any<Guid>());
    }

    /// <summary>
    /// Blocks the claim with a task that does not complete. It proves that the notifier is
    /// fire-and-forget: TryQueue returns while the claim is still blocked, and the notifier does not call the send.
    /// The SAML login path depends on this behavior, so a slow cache does not delay a login.
    /// </summary>
    [Fact]
    public async Task TryQueue_ReturnsBeforeClaimCompletes()
    {
        var organizationId = Guid.NewGuid();
        var gate = NewClaimGate();
        var claimStarted = NewSignal();
        _interval.TryClaimIntervalAsync(organizationId).Returns(_ =>
        {
            claimStarted.TrySetResult();
            return gate.Task;
        });

        _sut.TryQueue(organizationId);

        Assert.False(gate.Task.IsCompleted);
        await claimStarted.Task.WaitAsync(_wait);
        await Task.Delay(_negativeWait);
        await _command.DidNotReceive().SendAsync(Arg.Any<Guid>());

        gate.SetResult(false);
    }

    /// <summary>
    /// Blocks the claim for one organization, and then calls TryQueue 10 more times for that
    /// organization. It proves that the guard drops the repeat calls and that the interval receives one
    /// claim. This catches many claims from concurrent calls.
    /// </summary>
    [Fact]
    public async Task TryQueue_ConcurrentCallsForSameOrganization_ClaimOnlyOnce()
    {
        var organizationId = Guid.NewGuid();
        var gate = NewClaimGate();
        var claimStarted = NewSignal();
        _interval.TryClaimIntervalAsync(organizationId).Returns(_ =>
        {
            claimStarted.TrySetResult();
            return gate.Task;
        });

        _sut.TryQueue(organizationId);
        await claimStarted.Task.WaitAsync(_wait);

        for (var i = 0; i < 10; i++)
        {
            _sut.TryQueue(organizationId);
        }

        await Task.Delay(_negativeWait);
        await _interval.Received(1).TryClaimIntervalAsync(organizationId);

        gate.SetResult(false);
    }

    /// <summary>
    /// Blocks the claim for two organizations at the same time. It proves that each
    /// organization starts its own claim while the other claim is processing.
    /// </summary>
    [Fact]
    public async Task TryQueue_CallsForDifferentOrganizations_ClaimIndependently()
    {
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        var gate = NewClaimGate();
        var firstStarted = NewSignal();
        var secondStarted = NewSignal();
        _interval.TryClaimIntervalAsync(first).Returns(_ =>
        {
            firstStarted.TrySetResult();
            return gate.Task;
        });
        _interval.TryClaimIntervalAsync(second).Returns(_ =>
        {
            secondStarted.TrySetResult();
            return gate.Task;
        });

        _sut.TryQueue(first);
        _sut.TryQueue(second);

        await Task.WhenAll(firstStarted.Task, secondStarted.Task).WaitAsync(_wait);
        gate.SetResult(false);

        await _interval.Received(1).TryClaimIntervalAsync(first);
        await _interval.Received(1).TryClaimIntervalAsync(second);
    }

    /// <summary>
    /// Arranges a claim that returns false, and then calls TryQueue again until the interval
    /// receives a second claim. It proves that the notifier releases the guard after the first claim
    /// returns. This catches an organization that the notifier never claims again.
    /// </summary>
    [Fact]
    public async Task TryQueue_AfterClaimCompletes_AllowsNextClaimForSameOrganization()
    {
        var organizationId = Guid.NewGuid();
        _interval.TryClaimIntervalAsync(organizationId).Returns(false);

        _sut.TryQueue(organizationId);
        await QueueUntilClaimCountAsync(organizationId, 2);

        await _interval.Received(2).TryClaimIntervalAsync(organizationId);
    }

    /// <summary>
    /// Arranges a claim that throws an exception. It proves that the notifier releases the
    /// guard and that the notifier does not call the send. It also proves that the notifier logs an
    /// error message that does not contain the organization identifier.
    /// </summary>
    [Fact]
    public async Task TryQueue_ClaimThrows_ReleasesGuardAndDoesNotPropagate()
    {
        var organizationId = Guid.NewGuid();
        var thrownException = new InvalidOperationException("claim failed");
        _interval.TryClaimIntervalAsync(organizationId).Throws(thrownException);

        var exception = Record.Exception(() => _sut.TryQueue(organizationId));
        Assert.Null(exception);

        await QueueUntilClaimCountAsync(organizationId, 2);
        await WaitForLogCountAsync(2);

        await _interval.Received(2).TryClaimIntervalAsync(organizationId);
        await _command.DidNotReceive().SendAsync(Arg.Any<Guid>());
        _logger.Received(2).Log(
            LogLevel.Error,
            Arg.Any<EventId>(),
            Arg.Is<object>(state =>
                state.ToString() == "Failed to claim or send the RSA 1.5 deprecation email." &&
                !state.ToString()!.Contains(organizationId.ToString())),
            thrownException,
            Arg.Any<Func<object, Exception?, string>>());
    }

    /// <summary>
    /// Arranges a claim that returns true and a send that fails. It proves that the notifier calls
    /// the send. It also proves that the send failure stays on the background task.
    /// </summary>
    [Fact]
    public async Task TryQueue_CommandThrows_DoesNotPropagate()
    {
        var organizationId = Guid.NewGuid();
        var sendCalled = NewSignal();
        _interval.TryClaimIntervalAsync(organizationId).Returns(true);
        _command.SendAsync(organizationId).Returns(_ =>
        {
            sendCalled.TrySetResult();
            return Task.FromException(new InvalidOperationException("send failed"));
        });

        var exception = Record.Exception(() => _sut.TryQueue(organizationId));

        Assert.Null(exception);
        await sendCalled.Task.WaitAsync(_wait);
    }
}
