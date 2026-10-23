using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.PrivilegedControlsSeats;
using Bit.Core.Billing.Models.Mail.Mailer;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Models.Data.Organizations.OrganizationUsers;
using Bit.Core.Platform.Mail.Mailer;
using Bit.Core.Repositories;
using Bit.Core.Utilities;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace Bit.Core.Test.AdminConsole.OrganizationFeatures.OrganizationUsers.PrivilegedControlsSeats;

[SutProviderCustomize]
public class ReservePrivilegedControlsSeatForJoiningMemberCommandTests
{
    private const string _memberEmail = "member@example.com";

    [Theory]
    [BitAutoData]
    public async Task RunAsync_MemberWithoutAccess_ReturnsTrueWithoutReserving(
        Organization organization, OrganizationUser organizationUser,
        SutProvider<ReservePrivilegedControlsSeatForJoiningMemberCommand> sutProvider)
    {
        organization.UsePam = true;
        organizationUser.AccessPam = false;

        var result = await sutProvider.Sut.RunAsync(organization, organizationUser, _memberEmail);

        Assert.True(result);
        await sutProvider.GetDependency<IReservePrivilegedControlsSeatsCommand>()
            .DidNotReceiveWithAnyArgs().RunAsync(default!, default);
    }

    [Theory]
    [BitAutoData]
    public async Task RunAsync_PamOff_KeepsAccessWithoutEmail(
        Organization organization, OrganizationUser organizationUser,
        SutProvider<ReservePrivilegedControlsSeatForJoiningMemberCommand> sutProvider)
    {
        organization.UsePam = false;
        organizationUser.AccessPam = true;

        var result = await sutProvider.Sut.RunAsync(organization, organizationUser, _memberEmail);

        Assert.True(result);
        Assert.True(organizationUser.AccessPam);
        await sutProvider.GetDependency<IReservePrivilegedControlsSeatsCommand>()
            .DidNotReceiveWithAnyArgs().RunAsync(default!, default);
        await sutProvider.GetDependency<IMailer>()
            .DidNotReceiveWithAnyArgs().SendEmail<PrivilegedControlsSeatNeededView>(default!);
    }

    [Theory]
    [BitAutoData]
    public async Task RunAsync_Reserved_KeepsAccessAndSendsNoEmail(
        Organization organization, OrganizationUser organizationUser,
        SutProvider<ReservePrivilegedControlsSeatForJoiningMemberCommand> sutProvider)
    {
        organization.UsePam = true;
        organizationUser.AccessPam = true;
        sutProvider.GetDependency<IReservePrivilegedControlsSeatsCommand>()
            .RunAsync(organization, 1).Returns(PrivilegedControlsSeatReservation.Reserved);

        var result = await sutProvider.Sut.RunAsync(organization, organizationUser, _memberEmail);

        Assert.True(result);
        Assert.True(organizationUser.AccessPam);
        await sutProvider.GetDependency<IMailer>()
            .DidNotReceiveWithAnyArgs().SendEmail<PrivilegedControlsSeatNeededView>(default!);
    }

    [Theory]
    [BitAutoData(PrivilegedControlsSeatReservation.LimitReached)]
    [BitAutoData(PrivilegedControlsSeatReservation.SelfHostedShortfall)]
    public async Task RunAsync_NoSeatAvailable_RemovesAccessAndReturnsFalse(
        PrivilegedControlsSeatReservation reservation, Organization organization, OrganizationUser organizationUser,
        SutProvider<ReservePrivilegedControlsSeatForJoiningMemberCommand> sutProvider)
    {
        SetUpNoSeat(reservation, organization, organizationUser, sutProvider, ["owner@example.com"]);

        var result = await sutProvider.Sut.RunAsync(organization, organizationUser, _memberEmail);

        Assert.False(result);
        Assert.False(organizationUser.AccessPam);
    }

    [Theory]
    [BitAutoData(PrivilegedControlsSeatReservation.LimitReached)]
    [BitAutoData(PrivilegedControlsSeatReservation.SelfHostedShortfall)]
    public async Task RunAsync_NoSeatAvailable_EmailsOwners(
        PrivilegedControlsSeatReservation reservation, Organization organization, OrganizationUser organizationUser,
        SutProvider<ReservePrivilegedControlsSeatForJoiningMemberCommand> sutProvider)
    {
        SetUpNoSeat(reservation, organization, organizationUser, sutProvider,
            ["owner1@example.com", "owner2@example.com", "owner1@example.com"]);

        await sutProvider.Sut.RunAsync(organization, organizationUser, _memberEmail);

        await sutProvider.GetDependency<IMailer>().Received(1).SendEmail(Arg.Is<PrivilegedControlsSeatNeededMail>(mail =>
            mail.ToEmails.SequenceEqual(new[] { "owner1@example.com", "owner2@example.com" })
            && mail.View.MemberEmail == CoreHelpers.PreventEmailAutoLinking(_memberEmail)
            && mail.View.OrganizationId == organization.Id));
    }

    [Theory]
    [BitAutoData]
    public async Task RunAsync_LimitReached_RemovesAccessAndEmailsOwners(
        Organization organization, OrganizationUser organizationUser,
        SutProvider<ReservePrivilegedControlsSeatForJoiningMemberCommand> sutProvider)
    {
        SetUpNoSeat(PrivilegedControlsSeatReservation.LimitReached, organization, organizationUser, sutProvider,
            ["owner@example.com"]);

        var result = await sutProvider.Sut.RunAsync(organization, organizationUser, _memberEmail);

        Assert.False(result);
        Assert.False(organizationUser.AccessPam);
        await sutProvider.GetDependency<IMailer>().Received(1).SendEmail(Arg.Is<PrivilegedControlsSeatNeededMail>(mail =>
            mail.ToEmails.SequenceEqual(new[] { "owner@example.com" })));
    }

    [Theory]
    [BitAutoData]
    public async Task RunAsync_EmailFails_StillReturnsFalseWithoutThrowing(
        Organization organization, OrganizationUser organizationUser,
        SutProvider<ReservePrivilegedControlsSeatForJoiningMemberCommand> sutProvider)
    {
        SetUpNoSeat(PrivilegedControlsSeatReservation.LimitReached, organization, organizationUser, sutProvider,
            ["owner@example.com"]);
        sutProvider.GetDependency<IMailer>().SendEmail<PrivilegedControlsSeatNeededView>(default!).ThrowsAsyncForAnyArgs(new Exception("smtp down"));

        var result = await sutProvider.Sut.RunAsync(organization, organizationUser, _memberEmail);

        Assert.False(result);
        Assert.False(organizationUser.AccessPam);
    }

    private static void SetUpNoSeat(PrivilegedControlsSeatReservation reservation, Organization organization,
        OrganizationUser organizationUser,
        SutProvider<ReservePrivilegedControlsSeatForJoiningMemberCommand> sutProvider, string[] ownerEmails)
    {
        organization.UsePam = true;
        organizationUser.AccessPam = true;
        sutProvider.GetDependency<IReservePrivilegedControlsSeatsCommand>()
            .RunAsync(organization, 1).Returns(reservation);
        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyByMinimumRoleAsync(organization.Id, OrganizationUserType.Owner)
            .Returns(ownerEmails.Select(email => new OrganizationUserUserDetails { Email = email }).ToList());
    }
}
