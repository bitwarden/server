using Bit.Core;
using Bit.Core.Entities;
using Bit.Core.Platform.Mail.Mailer;
using Bit.Core.Repositories;
using Bit.Services.Pam.Services;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Bitwarden.Server.Sdk.Features;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace Bit.Services.Pam.Test.Services;

[SutProviderCustomize]
public class AccessMailNotifierTests
{
    private static readonly DateTime _now = new(2026, 6, 4, 12, 0, 0, DateTimeKind.Utc);

    /// <summary>The per-send delay a delivery service spends retrying before it reports anything.</summary>
    private static readonly TimeSpan _retryDelay = TimeSpan.FromSeconds(2);

    /// <summary>Stands in for a shipped mail; the notifier is indifferent to which mail travels through it.</summary>
    private class TestMailView : BaseMailView;

    private class TestMail : BaseMail<TestMailView>
    {
        public override string Subject { get; set; } = "Access request";
    }

    private static Func<string, BaseMail<TestMailView>> Build() =>
        email => new TestMail { ToEmails = [email], View = new TestMailView() };

    private static SutProvider<AccessMailNotifier> Setup()
    {
        var sutProvider = new SutProvider<AccessMailNotifier>().WithFakeTimeProvider().Create();
        sutProvider.GetDependency<FakeTimeProvider>().SetUtcNow(_now);
        return sutProvider;
    }

    private static void EnableFlag(SutProvider<AccessMailNotifier> sutProvider) =>
        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.Pam)
            .Returns(true);

    private static List<User> Recipients(int count) =>
        Enumerable.Range(0, count)
            .Select(i => new User { Id = Guid.NewGuid(), Email = $"approver{i}@example.com" })
            .ToList();

    private static void ResolveTo(SutProvider<AccessMailNotifier> sutProvider, IEnumerable<User> recipients) =>
        sutProvider.GetDependency<IUserRepository>()
            .GetManyAsync(Arg.Any<IEnumerable<Guid>>())
            .Returns(recipients);

    /// <summary>Every send fails the way a dead delivery service does: after it has spent its retry delay.</summary>
    private static void FailEverySendSlowly(SutProvider<AccessMailNotifier> sutProvider)
    {
        var time = sutProvider.GetDependency<FakeTimeProvider>();
        sutProvider.GetDependency<IMailer>()
            .SendEmail(Arg.Any<BaseMail<TestMailView>>())
            .Returns(_ =>
            {
                time.Advance(_retryDelay);
                return Task.FromException(new InvalidOperationException("delivery service unavailable"));
            });
    }

    [Theory, BitAutoData]
    public async Task SendToUserAsync_FlagOff_SendsNothingAndReadsNoUser(Guid userId)
    {
        var sutProvider = Setup();

        await sutProvider.Sut.SendToUserAsync(userId, Build());

        await sutProvider.GetDependency<IMailer>().DidNotReceiveWithAnyArgs()
            .SendEmail(Arg.Any<BaseMail<TestMailView>>());
        await sutProvider.GetDependency<IUserRepository>().DidNotReceiveWithAnyArgs().GetByIdAsync(default);
    }

    [Theory, BitAutoData]
    public async Task SendToUserAsync_FlagOn_SendsToTheResolvedAddress(User user)
    {
        var sutProvider = Setup();
        EnableFlag(sutProvider);
        sutProvider.GetDependency<IUserRepository>().GetByIdAsync(user.Id).Returns(user);

        await sutProvider.Sut.SendToUserAsync(user.Id, Build());

        await sutProvider.GetDependency<IMailer>().Received(1)
            .SendEmail(Arg.Is<BaseMail<TestMailView>>(m => m.ToEmails.Single() == user.Email));
    }

    [Theory, BitAutoData]
    public async Task SendToUserAsync_MailerThrows_DoesNotPropagate(User user)
    {
        var sutProvider = Setup();
        EnableFlag(sutProvider);
        sutProvider.GetDependency<IUserRepository>().GetByIdAsync(user.Id).Returns(user);
        sutProvider.GetDependency<IMailer>()
            .SendEmail(Arg.Any<BaseMail<TestMailView>>())
            .ThrowsAsync(new InvalidOperationException("delivery service unavailable"));

        var exception = await Record.ExceptionAsync(() => sutProvider.Sut.SendToUserAsync(user.Id, Build()));

        Assert.Null(exception);
    }

    [Theory, BitAutoData]
    public async Task SendToUserAsync_UserReadThrows_DoesNotPropagate(Guid userId)
    {
        var sutProvider = Setup();
        EnableFlag(sutProvider);
        sutProvider.GetDependency<IUserRepository>().GetByIdAsync(userId)
            .ThrowsAsync(new TimeoutException("database unavailable"));

        var exception = await Record.ExceptionAsync(() => sutProvider.Sut.SendToUserAsync(userId, Build()));

        Assert.Null(exception);
    }

    [Theory, BitAutoData]
    public async Task SendToUserAsync_UnknownUser_SendsNothing(Guid userId)
    {
        var sutProvider = Setup();
        EnableFlag(sutProvider);
        sutProvider.GetDependency<IUserRepository>().GetByIdAsync(userId).Returns((User?)null);

        await sutProvider.Sut.SendToUserAsync(userId, Build());

        await sutProvider.GetDependency<IMailer>().DidNotReceiveWithAnyArgs()
            .SendEmail(Arg.Any<BaseMail<TestMailView>>());
    }

    [Theory, BitAutoData]
    public async Task SendToUsersAsync_FlagOff_SendsNothing(Guid userA, Guid userB)
    {
        var sutProvider = Setup();

        await sutProvider.Sut.SendToUsersAsync([userA, userB], Build());

        await sutProvider.GetDependency<IMailer>().DidNotReceiveWithAnyArgs()
            .SendEmail(Arg.Any<BaseMail<TestMailView>>());
        await sutProvider.GetDependency<IUserRepository>().DidNotReceiveWithAnyArgs()
            .GetManyAsync(Arg.Any<IEnumerable<Guid>>());
    }

    [Theory, BitAutoData]
    public async Task SendToUsersAsync_FlagOn_SendsOneMailPerRecipient(User userA, User userB)
    {
        var sutProvider = Setup();
        EnableFlag(sutProvider);
        ResolveTo(sutProvider, [userA, userB]);

        await sutProvider.Sut.SendToUsersAsync([userA.Id, userB.Id], Build());

        await sutProvider.GetDependency<IMailer>().Received(1)
            .SendEmail(Arg.Is<BaseMail<TestMailView>>(m => m.ToEmails.Single() == userA.Email));
        await sutProvider.GetDependency<IMailer>().Received(1)
            .SendEmail(Arg.Is<BaseMail<TestMailView>>(m => m.ToEmails.Single() == userB.Email));
    }

    [Theory, BitAutoData]
    public async Task SendToUsersAsync_OneRecipientFails_TheRestStillReceive(User failing, User succeeding)
    {
        var sutProvider = Setup();
        EnableFlag(sutProvider);
        ResolveTo(sutProvider, [failing, succeeding]);
        sutProvider.GetDependency<IMailer>()
            .SendEmail(Arg.Is<BaseMail<TestMailView>>(m => m.ToEmails.Single() == failing.Email))
            .ThrowsAsync(new InvalidOperationException("delivery service unavailable"));

        var exception = await Record.ExceptionAsync(
            () => sutProvider.Sut.SendToUsersAsync([failing.Id, succeeding.Id], Build()));

        Assert.Null(exception);
        await sutProvider.GetDependency<IMailer>().Received(1)
            .SendEmail(Arg.Is<BaseMail<TestMailView>>(m => m.ToEmails.Single() == succeeding.Email));
    }

    [Theory, BitAutoData]
    public async Task SendToUsersAsync_RecipientReadThrows_DoesNotPropagate(Guid userA, Guid userB)
    {
        var sutProvider = Setup();
        EnableFlag(sutProvider);
        sutProvider.GetDependency<IUserRepository>()
            .GetManyAsync(Arg.Any<IEnumerable<Guid>>())
            .ThrowsAsync(new TimeoutException("database unavailable"));

        var exception = await Record.ExceptionAsync(() => sutProvider.Sut.SendToUsersAsync([userA, userB], Build()));

        Assert.Null(exception);
        await sutProvider.GetDependency<IMailer>().DidNotReceiveWithAnyArgs()
            .SendEmail(Arg.Any<BaseMail<TestMailView>>());
    }

    [Fact]
    public async Task SendToUsersAsync_DeliveryOutage_AbandonsTheBatchInsteadOfAttemptingEveryRecipient()
    {
        var sutProvider = Setup();
        EnableFlag(sutProvider);
        var recipients = Recipients(20);
        ResolveTo(sutProvider, recipients);
        FailEverySendSlowly(sutProvider);
        var startedAt = sutProvider.GetDependency<FakeTimeProvider>().GetTimestamp();

        var exception = await Record.ExceptionAsync(
            () => sutProvider.Sut.SendToUsersAsync(recipients.Select(r => r.Id), Build()));

        Assert.Null(exception);
        await sutProvider.GetDependency<IMailer>().Received(2)
            .SendEmail(Arg.Any<BaseMail<TestMailView>>());
        // The caller waits out the two attempts, not one retry delay per recipient.
        var elapsed = sutProvider.GetDependency<FakeTimeProvider>().GetElapsedTime(startedAt);
        Assert.Equal(_retryDelay * 2, elapsed);
    }

    [Fact]
    public async Task SendToUsersAsync_FailuresBrokenUpBySuccess_AttemptsEveryRecipient()
    {
        var sutProvider = Setup();
        EnableFlag(sutProvider);
        var recipients = Recipients(4);
        ResolveTo(sutProvider, recipients);
        // Alternating outcomes: an isolated bad address must not read as the delivery path being down.
        foreach (var failing in new[] { recipients[0], recipients[2] })
        {
            sutProvider.GetDependency<IMailer>()
                .SendEmail(Arg.Is<BaseMail<TestMailView>>(m => m.ToEmails.Single() == failing.Email))
                .ThrowsAsync(new InvalidOperationException("recipient rejected"));
        }

        await sutProvider.Sut.SendToUsersAsync(recipients.Select(r => r.Id), Build());

        await sutProvider.GetDependency<IMailer>().Received(4)
            .SendEmail(Arg.Any<BaseMail<TestMailView>>());
    }

    [Fact]
    public async Task SendToUsersAsync_LargeHealthyBatch_ReachesEveryRecipient()
    {
        var sutProvider = Setup();
        EnableFlag(sutProvider);
        // The managing-user set has no upper bound; a collection well above the typical size must still be
        // notified in full when delivery is healthy.
        var recipients = Recipients(100);
        ResolveTo(sutProvider, recipients);
        var time = sutProvider.GetDependency<FakeTimeProvider>();
        sutProvider.GetDependency<IMailer>()
            .SendEmail(Arg.Any<BaseMail<TestMailView>>())
            .Returns(_ =>
            {
                time.Advance(TimeSpan.FromMilliseconds(100));
                return Task.CompletedTask;
            });

        await sutProvider.Sut.SendToUsersAsync(recipients.Select(r => r.Id), Build());

        await sutProvider.GetDependency<IMailer>().Received(100)
            .SendEmail(Arg.Any<BaseMail<TestMailView>>());
    }

    [Fact]
    public async Task SendToUsersAsync_SendsThatSucceedSlowly_StopOnceTheBatchBudgetIsSpent()
    {
        var sutProvider = Setup();
        EnableFlag(sutProvider);
        var recipients = Recipients(40);
        ResolveTo(sutProvider, recipients);
        // The SendGrid path reports success after retrying, so only the time it burns bounds the batch.
        var time = sutProvider.GetDependency<FakeTimeProvider>();
        sutProvider.GetDependency<IMailer>()
            .SendEmail(Arg.Any<BaseMail<TestMailView>>())
            .Returns(_ =>
            {
                time.Advance(_retryDelay);
                return Task.CompletedTask;
            });

        await sutProvider.Sut.SendToUsersAsync(recipients.Select(r => r.Id), Build());

        // 30s of budget against a 2s cost per send, rather than all 40.
        await sutProvider.GetDependency<IMailer>().Received(15)
            .SendEmail(Arg.Any<BaseMail<TestMailView>>());
    }
}
