using Bit.Core.Auth.Sso;
using Bit.Sso.Utilities.Saml2;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace Bit.SSO.Test.Utilities;

/// <summary>
/// Tests for <see cref="Saml2Rsa15DeprecationNotifier"/>.
/// While an email send is pending for a given organization, <c>TryQueue</c> ignores more calls for that organization.
/// </summary>
public class Saml2Rsa15DeprecationNotifierTests
{
    private static readonly TimeSpan _wait = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan _negativeWait = TimeSpan.FromMilliseconds(250);

    private readonly ISaml2Rsa15DeprecationNoticeCommand _command = Substitute.For<ISaml2Rsa15DeprecationNoticeCommand>();
    private readonly ILogger<Saml2Rsa15DeprecationNotifier> _logger = Substitute.For<ILogger<Saml2Rsa15DeprecationNotifier>>();
    private readonly Saml2Rsa15DeprecationNotifier _sut;

    public Saml2Rsa15DeprecationNotifierTests()
    {
        _sut = new Saml2Rsa15DeprecationNotifier(_command, _logger);
    }

    private static TaskCompletionSource NewSignal() => new(TaskCreationOptions.RunContinuationsAsynchronously);

    // The notifier releases an organization from the guard after the email send returns or throws, so a repeat call
    // can be dropped until then. Repeat the call until the command receives the expected number of calls.
    private async Task QueueUntilCommandCallCountAsync(Guid organizationId, int expected)
    {
        var deadline = DateTime.UtcNow + _wait;
        while (_command.ReceivedCalls().Count() < expected)
        {
            if (DateTime.UtcNow > deadline)
            {
                throw new TimeoutException($"Expected {expected} command call(s).");
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
    /// Proves that the notifier calls the command for the same organization on a background task.
    /// </summary>
    [Fact]
    public async Task TryQueue_CallsCommandInBackground()
    {
        var organizationId = Guid.NewGuid();
        var called = NewSignal();
        _command.SendEmailIfDueAsync(organizationId).Returns(_ =>
        {
            called.TrySetResult();
            return Task.CompletedTask;
        });

        _sut.TryQueue(organizationId);

        await called.Task.WaitAsync(_wait);
        await _command.Received(1).SendEmailIfDueAsync(organizationId);
    }

    /// <summary>
    /// Blocks the email send with a task that does not complete. It proves that the notifier is
    /// fire-and-forget: TryQueue returns while the email send is still blocked.
    /// The SAML login path depends on this behavior, so a slow email send does not delay a login.
    /// </summary>
    [Fact]
    public async Task TryQueue_ReturnsBeforeEmailSendCompletes()
    {
        var organizationId = Guid.NewGuid();
        var gate = NewSignal();
        var started = NewSignal();
        _command.SendEmailIfDueAsync(organizationId).Returns(_ =>
        {
            started.TrySetResult();
            return gate.Task;
        });

        _sut.TryQueue(organizationId);

        Assert.False(gate.Task.IsCompleted);
        await started.Task.WaitAsync(_wait);

        gate.SetResult();
    }

    /// <summary>
    /// Blocks the email send for one organization, and then calls TryQueue 10 more times for that
    /// organization. It proves that the guard drops the repeat calls and that the command receives one call.
    /// </summary>
    [Fact]
    public async Task TryQueue_ConcurrentCallsForSameOrganization_StartsOneEmailSend()
    {
        var organizationId = Guid.NewGuid();
        var gate = NewSignal();
        var started = NewSignal();
        _command.SendEmailIfDueAsync(organizationId).Returns(_ =>
        {
            started.TrySetResult();
            return gate.Task;
        });

        _sut.TryQueue(organizationId);
        await started.Task.WaitAsync(_wait);

        for (var i = 0; i < 10; i++)
        {
            _sut.TryQueue(organizationId);
        }

        await Task.Delay(_negativeWait);
        await _command.Received(1).SendEmailIfDueAsync(organizationId);

        gate.SetResult();
    }

    /// <summary>
    /// Blocks the email send for two organizations at the same time. It proves that each
    /// organization starts its own email send while the other email send is pending.
    /// </summary>
    [Fact]
    public async Task TryQueue_CallsForDifferentOrganizations_StartIndependentEmailSends()
    {
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        var gate = NewSignal();
        var firstStarted = NewSignal();
        var secondStarted = NewSignal();
        _command.SendEmailIfDueAsync(first).Returns(_ =>
        {
            firstStarted.TrySetResult();
            return gate.Task;
        });
        _command.SendEmailIfDueAsync(second).Returns(_ =>
        {
            secondStarted.TrySetResult();
            return gate.Task;
        });

        _sut.TryQueue(first);
        _sut.TryQueue(second);

        await Task.WhenAll(firstStarted.Task, secondStarted.Task).WaitAsync(_wait);
        gate.SetResult();

        await _command.Received(1).SendEmailIfDueAsync(first);
        await _command.Received(1).SendEmailIfDueAsync(second);
    }

    /// <summary>
    /// Calls TryQueue again until the command receives a second call. It proves that the notifier releases
    /// the guard after the first email send returns. This catches an organization that the notifier never
    /// notifies again.
    /// </summary>
    [Fact]
    public async Task TryQueue_AfterEmailSendCompletes_AllowsNextEmailSendForSameOrganization()
    {
        var organizationId = Guid.NewGuid();
        _command.SendEmailIfDueAsync(organizationId).Returns(Task.CompletedTask);

        _sut.TryQueue(organizationId);
        await QueueUntilCommandCallCountAsync(organizationId, 2);

        await _command.Received(2).SendEmailIfDueAsync(organizationId);
    }

    /// <summary>
    /// Arranges a command that throws an exception. It proves that the exception stays on the background task,
    /// and that the notifier releases the guard. It also proves that the notifier logs an error message that does
    /// not contain the organization identifier.
    /// </summary>
    [Fact]
    public async Task TryQueue_CommandThrows_ReleasesGuardAndDoesNotPropagate()
    {
        var organizationId = Guid.NewGuid();
        var thrownException = new InvalidOperationException("send failed");
        _command.SendEmailIfDueAsync(organizationId).Throws(thrownException);

        var exception = Record.Exception(() => _sut.TryQueue(organizationId));
        Assert.Null(exception);

        await QueueUntilCommandCallCountAsync(organizationId, 2);
        await WaitForLogCountAsync(2);

        await _command.Received(2).SendEmailIfDueAsync(organizationId);
        _logger.Received(2).Log(
            LogLevel.Error,
            Arg.Any<EventId>(),
            Arg.Is<object>(state =>
                state.ToString() == "Failed to run the background RSA 1.5 deprecation email send." &&
                !state.ToString()!.Contains(organizationId.ToString())),
            thrownException,
            Arg.Any<Func<object, Exception?, string>>());
    }
}
