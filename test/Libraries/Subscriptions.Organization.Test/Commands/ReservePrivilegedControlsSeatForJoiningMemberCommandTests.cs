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
using Bit.Subscriptions.Organization.Commands;
using Bitwarden.Server.Sdk.Environment;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;
using OrganizationEntity = Bit.Core.AdminConsole.Entities.Organization;

namespace Bit.Subscriptions.Organization.Test.Commands;

public class ReservePrivilegedControlsSeatForJoiningMemberCommandTests
{
    private const string _memberEmail = "member@example.com";

    private readonly IReservePrivilegedControlsSeatsCommand _reservePrivilegedControlsSeatsCommand =
        Substitute.For<IReservePrivilegedControlsSeatsCommand>();
    private readonly IOrganizationUserRepository _organizationUserRepository =
        Substitute.For<IOrganizationUserRepository>();
    private readonly IMailer _mailer = Substitute.For<IMailer>();
    private readonly GlobalSettings _globalSettings = new();
    private readonly IBitwardenEnvironment _environment = Substitute.For<IBitwardenEnvironment>();
    private readonly ReservePrivilegedControlsSeatForJoiningMemberCommand _sut;

    public ReservePrivilegedControlsSeatForJoiningMemberCommandTests() =>
        _sut = new ReservePrivilegedControlsSeatForJoiningMemberCommand(
            _reservePrivilegedControlsSeatsCommand,
            _organizationUserRepository,
            _mailer,
            _globalSettings,
            _environment,
            new RecordingLogger<ReservePrivilegedControlsSeatForJoiningMemberCommand>());

    [Fact]
    public async Task RunAsync_MemberWithoutAccess_KeepsWithoutReserving()
    {
        var organization = CreateOrganization();
        var organizationUser = CreateOrganizationUser(accessPam: false);

        var result = await _sut.RunAsync(organization, organizationUser);

        Assert.Equal(JoiningMemberPrivilegedControlsAccess.Kept, result);
        await _reservePrivilegedControlsSeatsCommand.DidNotReceiveWithAnyArgs().RunAsync(default!, default);
    }

    [Fact]
    public async Task RunAsync_PamOff_KeepsAccessWithoutReserving()
    {
        var organization = CreateOrganization(usePam: false);
        var organizationUser = CreateOrganizationUser();

        var result = await _sut.RunAsync(organization, organizationUser);

        Assert.Equal(JoiningMemberPrivilegedControlsAccess.Kept, result);
        Assert.True(organizationUser.AccessPam);
        await _reservePrivilegedControlsSeatsCommand.DidNotReceiveWithAnyArgs().RunAsync(default!, default);
    }

    [Fact]
    public async Task RunAsync_Reserved_KeepsAccess()
    {
        var organization = CreateOrganization();
        var organizationUser = CreateOrganizationUser();
        _reservePrivilegedControlsSeatsCommand.RunAsync(organization, 1).Returns(PrivilegedControlsSeatReservation.Reserved);

        var result = await _sut.RunAsync(organization, organizationUser);

        Assert.Equal(JoiningMemberPrivilegedControlsAccess.Kept, result);
        Assert.True(organizationUser.AccessPam);
    }

    [Theory]
    [InlineData(PrivilegedControlsSeatReservation.LimitReached)]
    [InlineData(PrivilegedControlsSeatReservation.SelfHostedShortfall)]
    public async Task RunAsync_NoSeatAvailable_RemovesAccessAtSeatLimit(PrivilegedControlsSeatReservation reservation)
    {
        var organization = CreateOrganization();
        var organizationUser = CreateOrganizationUser();
        _reservePrivilegedControlsSeatsCommand.RunAsync(organization, 1).Returns(reservation);

        var result = await _sut.RunAsync(organization, organizationUser);

        Assert.Equal(JoiningMemberPrivilegedControlsAccess.RemovedAtSeatLimit, result);
        Assert.False(organizationUser.AccessPam);
    }

    [Fact]
    public async Task RunAsync_ReservationThrows_RemovesAccessAfterError()
    {
        var organization = CreateOrganization();
        var organizationUser = CreateOrganizationUser();
        _reservePrivilegedControlsSeatsCommand.RunAsync(organization, 1)
            .ThrowsAsync(new GatewayException("Stripe is down."));

        var result = await _sut.RunAsync(organization, organizationUser);

        Assert.Equal(JoiningMemberPrivilegedControlsAccess.RemovedAfterError, result);
        Assert.False(organizationUser.AccessPam);
    }

    [Fact]
    public async Task RunAsync_NoSeatAvailable_SendsNoEmail()
    {
        var organization = CreateOrganization();
        var organizationUser = CreateOrganizationUser();
        _reservePrivilegedControlsSeatsCommand.RunAsync(organization, 1)
            .Returns(PrivilegedControlsSeatReservation.LimitReached);

        await _sut.RunAsync(organization, organizationUser);

        await _mailer.DidNotReceiveWithAnyArgs().SendEmail<PrivilegedControlsSeatNeededView>(default!);
    }

    [Fact]
    public async Task NotifyOwnersAsync_Cloud_EmailsEachOwnerOnce()
    {
        var organization = CreateOrganization();
        SetUpOwners(organization, ["owner1@example.com", "owner2@example.com", "owner1@example.com"]);

        await _sut.NotifyOwnersAsync(organization, _memberEmail);

        await _mailer.Received(1).SendEmail(Arg.Is<PrivilegedControlsSeatNeededMail>(mail =>
            mail.ToEmails.SequenceEqual(new[] { "owner1@example.com", "owner2@example.com" })
            && mail.View.MemberEmail == CoreHelpers.PreventEmailAutoLinking(_memberEmail)
            && mail.View.OrganizationId == organization.Id));
    }

    [Fact]
    public async Task NotifyOwnersAsync_SelfHosted_SendsNoEmail()
    {
        var organization = CreateOrganization();
        SetUpOwners(organization, ["owner@example.com"]);
        _environment.SelfHosted.Returns(true);

        await _sut.NotifyOwnersAsync(organization, _memberEmail);

        await _mailer.DidNotReceiveWithAnyArgs().SendEmail<PrivilegedControlsSeatNeededView>(default!);
    }

    [Fact]
    public async Task NotifyOwnersAsync_EmailFails_DoesNotThrow()
    {
        var organization = CreateOrganization();
        SetUpOwners(organization, ["owner@example.com"]);
        _mailer.SendEmail<PrivilegedControlsSeatNeededView>(default!)
            .ThrowsAsyncForAnyArgs(new Exception("smtp down"));

        var exception = await Record.ExceptionAsync(() => _sut.NotifyOwnersAsync(organization, _memberEmail));

        Assert.Null(exception);
    }

    private void SetUpOwners(OrganizationEntity organization, string[] ownerEmails) =>
        _organizationUserRepository
            .GetManyByMinimumRoleAsync(organization.Id, OrganizationUserType.Owner)
            .Returns(ownerEmails.Select(email => new OrganizationUserUserDetails { Email = email }).ToList());

    private static OrganizationEntity CreateOrganization(bool usePam = true) => new()
    {
        Id = Guid.NewGuid(),
        Name = "Acme.Corp",
        UsePam = usePam,
        PamSeats = 5
    };

    private static OrganizationUser CreateOrganizationUser(bool accessPam = true) => new()
    {
        Id = Guid.NewGuid(),
        AccessPam = accessPam
    };
}
