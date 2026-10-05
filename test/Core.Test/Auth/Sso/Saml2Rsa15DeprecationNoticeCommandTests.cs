using Bit.Core.Auth.Sso;
using Bit.Core.Auth.Sso.Mail;
using Bit.Core.Enums;
using Bit.Core.Models.Data.Organizations.OrganizationUsers;
using Bit.Core.Platform.Mail.Mailer;
using Bit.Core.Repositories;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace Bit.Core.Test.Auth.Sso;

[SutProviderCustomize]
public class Saml2Rsa15DeprecationNoticeCommandTests
{
    private static void SetupAdmins(
        SutProvider<Saml2Rsa15DeprecationNoticeCommand> sutProvider,
        Guid organizationId,
        params string?[] emails) =>
        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyByMinimumRoleAsync(organizationId, OrganizationUserType.Admin)
            .Returns(emails.Select(e => new OrganizationUserUserDetails { Email = e! }).ToArray());

    [Theory, BitAutoData]
    public async Task SendAsync_OwnersAndAdmins_SendsOneMailToAllAddresses(
        SutProvider<Saml2Rsa15DeprecationNoticeCommand> sutProvider,
        Guid organizationId)
    {
        string[] addresses = ["owner@example.com", "admin@example.com", "other@example.com"];
        SetupAdmins(sutProvider, organizationId, addresses);

        await sutProvider.Sut.SendAsync(organizationId);

        await sutProvider.GetDependency<IMailer>()
            .Received(1)
            .SendEmail(Arg.Is<Saml2Rsa15DeprecationMail>(mail => mail.ToEmails.SequenceEqual(addresses)));
    }

    [Theory, BitAutoData]
    public async Task SendAsync_QueriesTheAdminMinimumRoleForTheGivenOrganization(
        SutProvider<Saml2Rsa15DeprecationNoticeCommand> sutProvider,
        Guid organizationId)
    {
        SetupAdmins(sutProvider, organizationId, "admin@example.com");

        await sutProvider.Sut.SendAsync(organizationId);

        await sutProvider.GetDependency<IOrganizationUserRepository>()
            .Received(1)
            .GetManyByMinimumRoleAsync(organizationId, OrganizationUserType.Admin);
    }

    [Theory, BitAutoData]
    public async Task SendAsync_NullAndBlankEmails_AreDropped(
        SutProvider<Saml2Rsa15DeprecationNoticeCommand> sutProvider,
        Guid organizationId)
    {
        SetupAdmins(sutProvider, organizationId, null, "", "   ", "admin@example.com");

        await sutProvider.Sut.SendAsync(organizationId);

        await sutProvider.GetDependency<IMailer>()
            .Received(1)
            .SendEmail(Arg.Is<Saml2Rsa15DeprecationMail>(mail =>
                mail.ToEmails.SequenceEqual(new[] { "admin@example.com" })));
    }

    [Theory, BitAutoData]
    public async Task SendAsync_DuplicateEmailsIgnoringCase_AreSentOnce(
        SutProvider<Saml2Rsa15DeprecationNoticeCommand> sutProvider,
        Guid organizationId)
    {
        SetupAdmins(sutProvider, organizationId, "a@x.com", "A@X.COM", "b@x.com");

        await sutProvider.Sut.SendAsync(organizationId);

        await sutProvider.GetDependency<IMailer>()
            .Received(1)
            .SendEmail(Arg.Is<Saml2Rsa15DeprecationMail>(mail =>
                mail.ToEmails.SequenceEqual(new[] { "a@x.com", "b@x.com" })));
    }

    [Theory, BitAutoData]
    public async Task SendAsync_NoRecipients_SendsNothing(
        SutProvider<Saml2Rsa15DeprecationNoticeCommand> sutProvider,
        Guid organizationId)
    {
        SetupAdmins(sutProvider, organizationId);

        await sutProvider.Sut.SendAsync(organizationId);

        SetupAdmins(sutProvider, organizationId, null, "", "   ");

        await sutProvider.Sut.SendAsync(organizationId);

        await sutProvider.GetDependency<IMailer>()
            .DidNotReceiveWithAnyArgs()
            .SendEmail<Saml2Rsa15DeprecationMailView>(default!);
    }

    [Theory, BitAutoData]
    public async Task SendAsync_RepositoryThrows_DoesNotPropagateAndSendsNothing(
        SutProvider<Saml2Rsa15DeprecationNoticeCommand> sutProvider,
        Guid organizationId)
    {
        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyByMinimumRoleAsync(organizationId, OrganizationUserType.Admin)
            .ThrowsAsync(new InvalidOperationException("repository failure"));

        await sutProvider.Sut.SendAsync(organizationId);

        await sutProvider.GetDependency<IMailer>()
            .DidNotReceiveWithAnyArgs()
            .SendEmail<Saml2Rsa15DeprecationMailView>(default!);
    }

    [Theory, BitAutoData]
    public async Task SendAsync_MailerThrows_DoesNotPropagateAndLogsFixedMessageWithoutOrganizationId(
        SutProvider<Saml2Rsa15DeprecationNoticeCommand> sutProvider,
        Guid organizationId)
    {
        const string recipient = "admin@example.com";
        var thrownException = new InvalidOperationException("mailer failure");
        SetupAdmins(sutProvider, organizationId, recipient);
        sutProvider.GetDependency<IMailer>()
            .SendEmail(Arg.Any<Saml2Rsa15DeprecationMail>())
            .ThrowsAsync(thrownException);

        await sutProvider.Sut.SendAsync(organizationId);

        sutProvider.GetDependency<ILogger<Saml2Rsa15DeprecationNoticeCommand>>().Received(1).Log(
            LogLevel.Error,
            Arg.Any<EventId>(),
            Arg.Is<object>(state =>
                state.ToString() == "Failed to send the RSA 1.5 deprecation email." &&
                !state.ToString()!.Contains(organizationId.ToString()) &&
                !state.ToString()!.Contains(recipient)),
            thrownException,
            Arg.Any<Func<object, Exception?, string>>());
    }
}
