using Bit.Core.Billing.Models.Mail.Mailer;
using Bit.Core.Enums;
using Bit.Core.Models.Data.Organizations.OrganizationUsers;
using Bit.Core.Platform.Mail.Mailer;
using Bit.Core.Repositories;
using Bit.Core.Settings;
using Bit.Core.Utilities;
using Bit.Subscriptions.Organization.Commands.PrivilegedControls;
using Bitwarden.Server.Sdk.Environment;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;
using OrganizationEntity = Bit.Core.AdminConsole.Entities.Organization;

namespace Bit.Subscriptions.Organization.Test.Commands;

public class SendPrivilegedControlsSeatNeededEmailCommandTests
{
    private const string _memberEmail = "member@example.com";

    private readonly IOrganizationUserRepository _organizationUserRepository =
        Substitute.For<IOrganizationUserRepository>();
    private readonly IMailer _mailer = Substitute.For<IMailer>();
    private readonly GlobalSettings _globalSettings = new();
    private readonly IBitwardenEnvironment _environment = Substitute.For<IBitwardenEnvironment>();
    private readonly SendPrivilegedControlsSeatNeededEmailCommand _sut;

    public SendPrivilegedControlsSeatNeededEmailCommandTests() =>
        _sut = new SendPrivilegedControlsSeatNeededEmailCommand(
            _organizationUserRepository,
            _mailer,
            _globalSettings,
            _environment,
            new RecordingLogger<SendPrivilegedControlsSeatNeededEmailCommand>());

    [Fact]
    public async Task RunAsync_Cloud_EmailsEachOwnerOnce()
    {
        var organization = CreateOrganization();
        SetUpOwners(organization, ["owner1@example.com", "owner2@example.com", "owner1@example.com"]);

        await _sut.RunAsync(organization, _memberEmail);

        await _mailer.Received(1).SendEmail(Arg.Is<PrivilegedControlsSeatNeededMail>(mail =>
            mail.ToEmails.SequenceEqual(new[] { "owner1@example.com", "owner2@example.com" })
            && mail.View.MemberEmail == CoreHelpers.PreventEmailAutoLinking(_memberEmail)
            && mail.View.OrganizationId == organization.Id));
    }

    [Fact]
    public async Task RunAsync_SelfHosted_SendsNoEmail()
    {
        var organization = CreateOrganization();
        SetUpOwners(organization, ["owner@example.com"]);
        _environment.SelfHosted.Returns(true);

        await _sut.RunAsync(organization, _memberEmail);

        await _mailer.DidNotReceiveWithAnyArgs().SendEmail<PrivilegedControlsSeatNeededView>(default!);
    }

    [Fact]
    public async Task RunAsync_EmailFails_DoesNotThrow()
    {
        var organization = CreateOrganization();
        SetUpOwners(organization, ["owner@example.com"]);
        _mailer.SendEmail<PrivilegedControlsSeatNeededView>(default!)
            .ThrowsAsyncForAnyArgs(new Exception("smtp down"));

        var exception = await Record.ExceptionAsync(() => _sut.RunAsync(organization, _memberEmail));

        Assert.Null(exception);
    }

    private void SetUpOwners(OrganizationEntity organization, string[] ownerEmails) =>
        _organizationUserRepository
            .GetManyByMinimumRoleAsync(organization.Id, OrganizationUserType.Owner)
            .Returns(ownerEmails.Select(email => new OrganizationUserUserDetails { Email = email }).ToList());

    private static OrganizationEntity CreateOrganization() => new()
    {
        Id = Guid.NewGuid(),
        Name = "Acme.Corp",
        UsePam = true,
        PamSeats = 5
    };
}
