using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.PrivilegedControlsSeats;
using Bit.Core.Billing.Models.Mail.Mailer;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Models.Data.Organizations.OrganizationUsers;
using Bit.Core.Platform.Mail.Mailer;
using Bit.Core.Repositories;
using Bit.Core.Settings;
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
    public async Task RunAsync_MemberWithoutAccess_KeepsWithoutReserving(
        Organization organization, OrganizationUser organizationUser,
        SutProvider<ReservePrivilegedControlsSeatForJoiningMemberCommand> sutProvider)
    {
        organization.UsePam = true;
        organizationUser.AccessPam = false;

        var result = await sutProvider.Sut.RunAsync(organization, organizationUser);

        Assert.Equal(JoiningMemberPrivilegedControlsAccess.Kept, result);
        await sutProvider.GetDependency<IReservePrivilegedControlsSeatsCommand>()
            .DidNotReceiveWithAnyArgs().RunAsync(default!, default);
    }

    [Theory]
    [BitAutoData]
    public async Task RunAsync_PamOff_KeepsAccessWithoutReserving(
        Organization organization, OrganizationUser organizationUser,
        SutProvider<ReservePrivilegedControlsSeatForJoiningMemberCommand> sutProvider)
    {
        organization.UsePam = false;
        organizationUser.AccessPam = true;

        var result = await sutProvider.Sut.RunAsync(organization, organizationUser);

        Assert.Equal(JoiningMemberPrivilegedControlsAccess.Kept, result);
        Assert.True(organizationUser.AccessPam);
        await sutProvider.GetDependency<IReservePrivilegedControlsSeatsCommand>()
            .DidNotReceiveWithAnyArgs().RunAsync(default!, default);
    }

    [Theory]
    [BitAutoData]
    public async Task RunAsync_Reserved_KeepsAccess(
        Organization organization, OrganizationUser organizationUser,
        SutProvider<ReservePrivilegedControlsSeatForJoiningMemberCommand> sutProvider)
    {
        SetUpReservation(PrivilegedControlsSeatReservation.Reserved, organization, organizationUser, sutProvider);

        var result = await sutProvider.Sut.RunAsync(organization, organizationUser);

        Assert.Equal(JoiningMemberPrivilegedControlsAccess.Kept, result);
        Assert.True(organizationUser.AccessPam);
    }

    [Theory]
    [BitAutoData(PrivilegedControlsSeatReservation.LimitReached)]
    [BitAutoData(PrivilegedControlsSeatReservation.SelfHostedShortfall)]
    public async Task RunAsync_NoSeatAvailable_RemovesAccessAtSeatLimit(
        PrivilegedControlsSeatReservation reservation, Organization organization, OrganizationUser organizationUser,
        SutProvider<ReservePrivilegedControlsSeatForJoiningMemberCommand> sutProvider)
    {
        SetUpReservation(reservation, organization, organizationUser, sutProvider);

        var result = await sutProvider.Sut.RunAsync(organization, organizationUser);

        Assert.Equal(JoiningMemberPrivilegedControlsAccess.RemovedAtSeatLimit, result);
        Assert.False(organizationUser.AccessPam);
    }

    [Theory]
    [BitAutoData]
    public async Task RunAsync_ReservationThrows_RemovesAccessAfterError(
        Organization organization, OrganizationUser organizationUser,
        SutProvider<ReservePrivilegedControlsSeatForJoiningMemberCommand> sutProvider)
    {
        organization.UsePam = true;
        organizationUser.AccessPam = true;
        sutProvider.GetDependency<IReservePrivilegedControlsSeatsCommand>()
            .RunAsync(organization, 1).ThrowsAsync(new GatewayException("Stripe is down."));

        var result = await sutProvider.Sut.RunAsync(organization, organizationUser);

        Assert.Equal(JoiningMemberPrivilegedControlsAccess.RemovedAfterError, result);
        Assert.False(organizationUser.AccessPam);
    }

    [Theory]
    [BitAutoData]
    public async Task RunAsync_NoSeatAvailable_SendsNoEmail(
        Organization organization, OrganizationUser organizationUser,
        SutProvider<ReservePrivilegedControlsSeatForJoiningMemberCommand> sutProvider)
    {
        SetUpReservation(PrivilegedControlsSeatReservation.LimitReached, organization, organizationUser, sutProvider);

        await sutProvider.Sut.RunAsync(organization, organizationUser);

        await sutProvider.GetDependency<IMailer>()
            .DidNotReceiveWithAnyArgs().SendEmail<PrivilegedControlsSeatNeededView>(default!);
    }

    [Theory]
    [BitAutoData]
    public async Task NotifyOwnersAsync_Cloud_EmailsEachOwnerOnce(
        Organization organization, SutProvider<ReservePrivilegedControlsSeatForJoiningMemberCommand> sutProvider)
    {
        SetUpOwners(organization, sutProvider, ["owner1@example.com", "owner2@example.com", "owner1@example.com"]);

        await sutProvider.Sut.NotifyOwnersAsync(organization, _memberEmail);

        await sutProvider.GetDependency<IMailer>().Received(1).SendEmail(Arg.Is<PrivilegedControlsSeatNeededMail>(mail =>
            mail.ToEmails.SequenceEqual(new[] { "owner1@example.com", "owner2@example.com" })
            && mail.View.MemberEmail == CoreHelpers.PreventEmailAutoLinking(_memberEmail)
            && mail.View.OrganizationId == organization.Id));
    }

    [Theory]
    [BitAutoData]
    public async Task NotifyOwnersAsync_SelfHosted_SendsNoEmail(
        Organization organization, SutProvider<ReservePrivilegedControlsSeatForJoiningMemberCommand> sutProvider)
    {
        SetUpOwners(organization, sutProvider, ["owner@example.com"]);
        sutProvider.GetDependency<IGlobalSettings>().SelfHosted.Returns(true);

        await sutProvider.Sut.NotifyOwnersAsync(organization, _memberEmail);

        await sutProvider.GetDependency<IMailer>()
            .DidNotReceiveWithAnyArgs().SendEmail<PrivilegedControlsSeatNeededView>(default!);
    }

    [Theory]
    [BitAutoData]
    public async Task NotifyOwnersAsync_EmailFails_DoesNotThrow(
        Organization organization, SutProvider<ReservePrivilegedControlsSeatForJoiningMemberCommand> sutProvider)
    {
        SetUpOwners(organization, sutProvider, ["owner@example.com"]);
        sutProvider.GetDependency<IMailer>().SendEmail<PrivilegedControlsSeatNeededView>(default!)
            .ThrowsAsyncForAnyArgs(new Exception("smtp down"));

        var exception = await Record.ExceptionAsync(() => sutProvider.Sut.NotifyOwnersAsync(organization, _memberEmail));

        Assert.Null(exception);
    }

    private static void SetUpReservation(PrivilegedControlsSeatReservation reservation, Organization organization,
        OrganizationUser organizationUser, SutProvider<ReservePrivilegedControlsSeatForJoiningMemberCommand> sutProvider)
    {
        organization.UsePam = true;
        organizationUser.AccessPam = true;
        sutProvider.GetDependency<IReservePrivilegedControlsSeatsCommand>()
            .RunAsync(organization, 1).Returns(reservation);
    }

    private static void SetUpOwners(Organization organization,
        SutProvider<ReservePrivilegedControlsSeatForJoiningMemberCommand> sutProvider, string[] ownerEmails) =>
        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyByMinimumRoleAsync(organization.Id, OrganizationUserType.Owner)
            .Returns(ownerEmails.Select(email => new OrganizationUserUserDetails { Email = email }).ToList());
}
