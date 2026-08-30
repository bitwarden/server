using Bit.Core;
using Bit.Core.Entities;
using Bit.Core.Platform.Mail.Mailer;
using Bit.Core.Repositories;
using Bit.Services.Pam.Services;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Bitwarden.Server.Sdk.Features;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace Bit.Services.Pam.Test.Services;

[SutProviderCustomize]
public class AccessMailNotifierTests
{
    /// <summary>
    /// A stand-in for the mails later milestones will send. The notifier is a seam over <see cref="IMailer" /> and
    /// is deliberately indifferent to which mail travels through it, so the specs supply their own rather than
    /// coupling to a shipped one.
    /// </summary>
    private class TestMailView : BaseMailView;

    private class TestMail : BaseMail<TestMailView>
    {
        public override string Subject { get; set; } = "Access request";
    }

    private static Func<string, BaseMail<TestMailView>> Build() =>
        email => new TestMail { ToEmails = [email], View = new TestMailView() };

    private static void EnableFlag(SutProvider<AccessMailNotifier> sutProvider) =>
        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.PamEmailNotifications)
            .Returns(true);

    [Theory, BitAutoData]
    public async Task SendToUserAsync_FlagOff_SendsNothingAndReadsNoUser(
        SutProvider<AccessMailNotifier> sutProvider, Guid userId)
    {
        await sutProvider.Sut.SendToUserAsync(userId, Build());

        await sutProvider.GetDependency<IMailer>().DidNotReceiveWithAnyArgs()
            .SendEmail(Arg.Any<BaseMail<TestMailView>>());
        await sutProvider.GetDependency<IUserRepository>().DidNotReceiveWithAnyArgs().GetByIdAsync(default);
    }

    [Theory, BitAutoData]
    public async Task SendToUserAsync_FlagOn_SendsToTheResolvedAddress(
        SutProvider<AccessMailNotifier> sutProvider, User user)
    {
        EnableFlag(sutProvider);
        sutProvider.GetDependency<IUserRepository>().GetByIdAsync(user.Id).Returns(user);

        await sutProvider.Sut.SendToUserAsync(user.Id, Build());

        await sutProvider.GetDependency<IMailer>().Received(1)
            .SendEmail(Arg.Is<BaseMail<TestMailView>>(m => m.ToEmails.Single() == user.Email));
    }

    [Theory, BitAutoData]
    public async Task SendToUserAsync_MailerThrows_DoesNotPropagate(
        SutProvider<AccessMailNotifier> sutProvider, User user)
    {
        EnableFlag(sutProvider);
        sutProvider.GetDependency<IUserRepository>().GetByIdAsync(user.Id).Returns(user);
        sutProvider.GetDependency<IMailer>()
            .SendEmail(Arg.Any<BaseMail<TestMailView>>())
            .ThrowsAsync(new InvalidOperationException("delivery service unavailable"));

        var exception = await Record.ExceptionAsync(() => sutProvider.Sut.SendToUserAsync(user.Id, Build()));

        Assert.Null(exception);
    }

    [Theory, BitAutoData]
    public async Task SendToUserAsync_UserReadThrows_DoesNotPropagate(
        SutProvider<AccessMailNotifier> sutProvider, Guid userId)
    {
        EnableFlag(sutProvider);
        sutProvider.GetDependency<IUserRepository>().GetByIdAsync(userId)
            .ThrowsAsync(new TimeoutException("database unavailable"));

        var exception = await Record.ExceptionAsync(() => sutProvider.Sut.SendToUserAsync(userId, Build()));

        Assert.Null(exception);
    }

    [Theory, BitAutoData]
    public async Task SendToUserAsync_UnknownUser_SendsNothing(
        SutProvider<AccessMailNotifier> sutProvider, Guid userId)
    {
        EnableFlag(sutProvider);
        sutProvider.GetDependency<IUserRepository>().GetByIdAsync(userId).Returns((User?)null);

        await sutProvider.Sut.SendToUserAsync(userId, Build());

        await sutProvider.GetDependency<IMailer>().DidNotReceiveWithAnyArgs()
            .SendEmail(Arg.Any<BaseMail<TestMailView>>());
    }

    [Theory, BitAutoData]
    public async Task SendToUsersAsync_FlagOff_SendsNothing(
        SutProvider<AccessMailNotifier> sutProvider, Guid userA, Guid userB)
    {
        await sutProvider.Sut.SendToUsersAsync([userA, userB], Build());

        await sutProvider.GetDependency<IMailer>().DidNotReceiveWithAnyArgs()
            .SendEmail(Arg.Any<BaseMail<TestMailView>>());
        await sutProvider.GetDependency<IUserRepository>().DidNotReceiveWithAnyArgs()
            .GetManyAsync(Arg.Any<IEnumerable<Guid>>());
    }

    [Theory, BitAutoData]
    public async Task SendToUsersAsync_FlagOn_SendsOneMailPerRecipient(
        SutProvider<AccessMailNotifier> sutProvider, User userA, User userB)
    {
        EnableFlag(sutProvider);
        sutProvider.GetDependency<IUserRepository>()
            .GetManyAsync(Arg.Any<IEnumerable<Guid>>())
            .Returns([userA, userB]);

        await sutProvider.Sut.SendToUsersAsync([userA.Id, userB.Id], Build());

        await sutProvider.GetDependency<IMailer>().Received(1)
            .SendEmail(Arg.Is<BaseMail<TestMailView>>(m => m.ToEmails.Single() == userA.Email));
        await sutProvider.GetDependency<IMailer>().Received(1)
            .SendEmail(Arg.Is<BaseMail<TestMailView>>(m => m.ToEmails.Single() == userB.Email));
    }

    [Theory, BitAutoData]
    public async Task SendToUsersAsync_OneRecipientFails_TheRestStillReceive(
        SutProvider<AccessMailNotifier> sutProvider, User failing, User succeeding)
    {
        EnableFlag(sutProvider);
        sutProvider.GetDependency<IUserRepository>()
            .GetManyAsync(Arg.Any<IEnumerable<Guid>>())
            .Returns([failing, succeeding]);
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
    public async Task SendToUsersAsync_RecipientReadThrows_DoesNotPropagate(
        SutProvider<AccessMailNotifier> sutProvider, Guid userA, Guid userB)
    {
        EnableFlag(sutProvider);
        sutProvider.GetDependency<IUserRepository>()
            .GetManyAsync(Arg.Any<IEnumerable<Guid>>())
            .ThrowsAsync(new TimeoutException("database unavailable"));

        var exception = await Record.ExceptionAsync(() => sutProvider.Sut.SendToUsersAsync([userA, userB], Build()));

        Assert.Null(exception);
        await sutProvider.GetDependency<IMailer>().DidNotReceiveWithAnyArgs()
            .SendEmail(Arg.Any<BaseMail<TestMailView>>());
    }
}
