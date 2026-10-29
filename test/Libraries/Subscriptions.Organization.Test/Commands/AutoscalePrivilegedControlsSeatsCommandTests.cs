using Bit.Core.Billing.Commands;
using Bit.Core.Billing.Enums;
using Bit.Core.Billing.Models.Mail.Mailer;
using Bit.Core.Billing.Organizations.Commands;
using Bit.Core.Billing.Organizations.Helpers;
using Bit.Core.Billing.Organizations.Models;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Models.Data.Organizations.OrganizationUsers;
using Bit.Core.Platform.Mail.Mailer;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Settings;
using Bit.Core.Utilities;
using Bit.Subscriptions.Organization.Commands;
using Bitwarden.Server.Sdk.Environment;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Stripe;
using Xunit;
using OrganizationEntity = Bit.Core.AdminConsole.Entities.Organization;

namespace Bit.Subscriptions.Organization.Test.Commands;

public class AutoscalePrivilegedControlsSeatsCommandTests
{
    private const int _boughtSeats = 12;
    private const int _seatsInUse = 10;
    private const string _ownerEmail = "owner@example.com";

    private readonly IPrivilegedControlsSeatChangeSetFactory _seatChangeSetFactory =
        Substitute.For<IPrivilegedControlsSeatChangeSetFactory>();
    private readonly IUpdateOrganizationSubscriptionCommand _updateOrganizationSubscriptionCommand =
        Substitute.For<IUpdateOrganizationSubscriptionCommand>();
    private readonly IOrganizationService _organizationService = Substitute.For<IOrganizationService>();
    private readonly IOrganizationUserRepository _organizationUserRepository =
        Substitute.For<IOrganizationUserRepository>();
    private readonly IMailer _mailer = Substitute.For<IMailer>();
    private readonly GlobalSettings _globalSettings = new();
    private readonly IBitwardenEnvironment _environment = Substitute.For<IBitwardenEnvironment>();
    private readonly AutoscalePrivilegedControlsSeatsCommand _sut;

    public AutoscalePrivilegedControlsSeatsCommandTests() =>
        _sut = new AutoscalePrivilegedControlsSeatsCommand(
            _seatChangeSetFactory,
            _updateOrganizationSubscriptionCommand,
            _organizationService,
            _organizationUserRepository,
            _mailer,
            _globalSettings,
            _environment,
            new RecordingLogger<AutoscalePrivilegedControlsSeatsCommand>());

    [Fact]
    public async Task Run_SelfHosted_ThrowsBadRequest()
    {
        var organization = CreateOrganization();
        _environment.SelfHosted.Returns(true);

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => _sut.Run(organization, 1));

        Assert.Equal("Cannot update subscription on a self-hosted instance.", exception.Message);
        await _seatChangeSetFactory.DidNotReceive().CreateAsync(Arg.Any<OrganizationEntity>(), Arg.Any<int>());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task Run_SeatsToAddBelowOne_ThrowsBadRequestWithoutReadingSeatsInUse(int seatsToAdd)
    {
        var organization = CreateOrganization();

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => _sut.Run(organization, seatsToAdd));

        Assert.Equal("At least one Privileged Controls seat must be added.", exception.Message);
        await _organizationUserRepository.DidNotReceive()
            .GetOccupiedPamSeatCountByOrganizationIdAsync(Arg.Any<Guid>());
        await _seatChangeSetFactory.DidNotReceive().CreateAsync(Arg.Any<OrganizationEntity>(), Arg.Any<int>());
    }

    [Fact]
    public async Task Run_OrganizationHasNoPrivilegedControls_ThrowsBadRequest()
    {
        var organization = CreateOrganization(usePam: false, pamSeats: null);

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => _sut.Run(organization, 1));

        Assert.Equal("Your organization doesn't have Privileged Controls.", exception.Message);
        await _seatChangeSetFactory.DidNotReceive().CreateAsync(Arg.Any<OrganizationEntity>(), Arg.Any<int>());
    }

    [Fact]
    public async Task Run_OrganizationUsesPrivilegedControlsWithNoSeats_ThrowsBadRequest()
    {
        var organization = CreateOrganization(usePam: true, pamSeats: 0);

        await Assert.ThrowsAsync<BadRequestException>(() => _sut.Run(organization, 1));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public async Task Run_NoGatewaySubscription_ThrowsBadRequestWithoutReadingSeatsInUse(string? gatewaySubscriptionId)
    {
        var organization = CreateOrganization(gatewaySubscriptionId: gatewaySubscriptionId);

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => _sut.Run(organization, 1));

        Assert.Equal("No subscription found.", exception.Message);
        await _organizationUserRepository.DidNotReceive()
            .GetOccupiedPamSeatCountByOrganizationIdAsync(Arg.Any<Guid>());
        await _seatChangeSetFactory.DidNotReceive().CreateAsync(Arg.Any<OrganizationEntity>(), Arg.Any<int>());
    }

    [Fact]
    public async Task Run_BoughtSeatsAlreadyCoverTheTotal_SucceedsWithoutChangingAnything()
    {
        var organization = CreateOrganization();
        SetSeatsInUse(organization, _seatsInUse);

        var result = await _sut.Run(organization, 2);

        Assert.True(result.Success);
        Assert.Equal(_boughtSeats, organization.PamSeats);
        await _seatChangeSetFactory.DidNotReceive().CreateAsync(Arg.Any<OrganizationEntity>(), Arg.Any<int>());
        await _updateOrganizationSubscriptionCommand.DidNotReceive()
            .Run(Arg.Any<OrganizationEntity>(), Arg.Any<OrganizationSubscriptionChangeSet>(), Arg.Any<Subscription?>());
        await _organizationService.DidNotReceive()
            .ReplaceAndUpdateCacheAsync(Arg.Any<OrganizationEntity>(), Arg.Any<EventType?>());
    }

    [Fact]
    public async Task Run_LimitBelowBoughtSeatsButSeatsAlreadyCoverTheTotal_Succeeds()
    {
        var organization = CreateOrganization(maxAutoscalePamSeats: 5);
        SetSeatsInUse(organization, _seatsInUse);

        var result = await _sut.Run(organization, 1);

        Assert.True(result.Success);
        await _seatChangeSetFactory.DidNotReceive().CreateAsync(Arg.Any<OrganizationEntity>(), Arg.Any<int>());
    }

    [Fact]
    public async Task Run_TotalExceedsBoughtSeatsWithNoLimit_AsksTheSeatChangeStepForTheTotal()
    {
        var organization = CreateOrganization();
        SetSeatsInUse(organization, _seatsInUse);
        SucceedWith();

        var result = await _sut.Run(organization, 5);

        Assert.True(result.Success);
        await _seatChangeSetFactory.Received(1).CreateAsync(organization, 15);
    }

    [Fact]
    public async Task Run_TotalExceedsBoughtSeats_AppliesTheChangeSetFromTheSeatChangeStep()
    {
        var organization = CreateOrganization();
        SetSeatsInUse(organization, _seatsInUse);
        var changeSet = SucceedWith();

        await _sut.Run(organization, 5);

        await _updateOrganizationSubscriptionCommand.Received(1).Run(organization, changeSet);
    }

    [Fact]
    public async Task Run_TotalExceedsBoughtSeats_SavesTheNewSeatCount()
    {
        var organization = CreateOrganization(maxAutoscalePamSeats: 20);
        SetSeatsInUse(organization, _seatsInUse);
        SucceedWith();

        await _sut.Run(organization, 5);

        Assert.Equal(15, organization.PamSeats);
        Assert.Equal(20, organization.MaxAutoscalePamSeats);
        await _organizationService.Received(1).ReplaceAndUpdateCacheAsync(organization);
    }

    [Fact]
    public async Task Run_TotalExceedsBoughtSeatsWithinTheLimit_DoesNotEmailOwners()
    {
        var organization = CreateOrganization(maxAutoscalePamSeats: 20);
        SetSeatsInUse(organization, _seatsInUse);
        SucceedWith();

        await _sut.Run(organization, 5);

        await _mailer.DidNotReceive().SendEmail(Arg.Any<PrivilegedControlsSeatLimitReachedMail>());
    }

    [Fact]
    public async Task Run_TotalExceedsTheLimit_ReturnsAFailedResultCarryingTheLimit()
    {
        var organization = CreateOrganization(maxAutoscalePamSeats: 14);
        SetSeatsInUse(organization, _seatsInUse);

        var result = await _sut.Run(organization, 5);

        Assert.False(result.Success);
        Assert.Equal(14, result.MaxAutoscaleSeats);
    }

    [Fact]
    public async Task Run_TotalExceedsTheLimit_ChangesSavesAndEmailsNothing()
    {
        var organization = CreateOrganization(maxAutoscalePamSeats: 14);
        SetSeatsInUse(organization, _seatsInUse);

        await _sut.Run(organization, 5);

        Assert.Equal(_boughtSeats, organization.PamSeats);
        await _seatChangeSetFactory.DidNotReceive().CreateAsync(Arg.Any<OrganizationEntity>(), Arg.Any<int>());
        await _updateOrganizationSubscriptionCommand.DidNotReceive()
            .Run(Arg.Any<OrganizationEntity>(), Arg.Any<OrganizationSubscriptionChangeSet>(), Arg.Any<Subscription?>());
        await _organizationService.DidNotReceive()
            .ReplaceAndUpdateCacheAsync(Arg.Any<OrganizationEntity>(), Arg.Any<EventType?>());
        await _mailer.DidNotReceive().SendEmail(Arg.Any<PrivilegedControlsSeatLimitReachedMail>());
    }

    [Fact]
    public async Task Run_TotalEqualsTheLimitAndSeatsAreBought_EmailsOwnersTheLimit()
    {
        var organization = CreateOrganization(maxAutoscalePamSeats: 15);
        SetSeatsInUse(organization, _seatsInUse);
        SucceedWith();

        var result = await _sut.Run(organization, 5);

        Assert.True(result.Success);
        await _mailer.Received(1).SendEmail(Arg.Is<PrivilegedControlsSeatLimitReachedMail>(mail =>
            mail.ToEmails.Single() == _ownerEmail &&
            mail.Subject == "Acme.Corp Privileged Controls seat limit reached" &&
            mail.View.OrganizationId == organization.Id &&
            mail.View.OrganizationName == CoreHelpers.PreventEmailAutoLinking("Acme.Corp") &&
            mail.View.MaxSeatCount == 15));
    }

    [Fact]
    public async Task Run_TotalEqualsTheLimitAndNoSeatsAreBought_StillEmailsOwners()
    {
        var organization = CreateOrganization(pamSeats: 12, maxAutoscalePamSeats: 12);
        SetSeatsInUse(organization, 11);

        var result = await _sut.Run(organization, 1);

        Assert.True(result.Success);
        await _seatChangeSetFactory.DidNotReceive().CreateAsync(Arg.Any<OrganizationEntity>(), Arg.Any<int>());
        await _mailer.Received(1).SendEmail(Arg.Is<PrivilegedControlsSeatLimitReachedMail>(mail =>
            mail.View.MaxSeatCount == 12));
    }

    [Fact]
    public async Task Run_NoLimit_NeverEmailsOwners()
    {
        var organization = CreateOrganization(maxAutoscalePamSeats: null);
        SetSeatsInUse(organization, _seatsInUse);
        SucceedWith();

        await _sut.Run(organization, 5);

        await _mailer.DidNotReceive().SendEmail(Arg.Any<PrivilegedControlsSeatLimitReachedMail>());
    }

    [Fact]
    public async Task Run_EmailFails_StillSucceedsAndKeepsTheSavedSeats()
    {
        var organization = CreateOrganization(maxAutoscalePamSeats: 15);
        SetSeatsInUse(organization, _seatsInUse);
        SucceedWith();
        _mailer.SendEmail(Arg.Any<PrivilegedControlsSeatLimitReachedMail>())
            .ThrowsAsync(new InvalidOperationException("Mail is down."));

        var result = await _sut.Run(organization, 5);

        Assert.True(result.Success);
        Assert.Equal(15, organization.PamSeats);
        await _organizationService.Received(1).ReplaceAndUpdateCacheAsync(organization);
    }

    [Fact]
    public async Task Run_SeatChangeStepRejectsTheRequest_ThrowsItsExceptionAndChangesNothing()
    {
        var organization = CreateOrganization();
        SetSeatsInUse(organization, _seatsInUse);
        _seatChangeSetFactory.CreateAsync(organization, 15)
            .ThrowsAsync(new BadRequestException("Organization's plan does not support Privileged Controls."));

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => _sut.Run(organization, 5));

        Assert.Equal("Organization's plan does not support Privileged Controls.", exception.Message);
        Assert.Equal(_boughtSeats, organization.PamSeats);
        await _organizationService.DidNotReceive()
            .ReplaceAndUpdateCacheAsync(Arg.Any<OrganizationEntity>(), Arg.Any<EventType?>());
    }

    [Fact]
    public async Task Run_SubscriptionUpdateRejected_ThrowsBadRequestAndLeavesTheOrganizationUnsaved()
    {
        var organization = CreateOrganization(maxAutoscalePamSeats: 15);
        SetSeatsInUse(organization, _seatsInUse);
        var changeSet = SucceedWith();
        _updateOrganizationSubscriptionCommand.Run(organization, changeSet)
            .Returns(new BadRequest("Your card was declined."));

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => _sut.Run(organization, 5));

        Assert.Equal("Your card was declined.", exception.Message);
        Assert.Equal(_boughtSeats, organization.PamSeats);
        await _organizationService.DidNotReceive()
            .ReplaceAndUpdateCacheAsync(Arg.Any<OrganizationEntity>(), Arg.Any<EventType?>());
        await _mailer.DidNotReceive().SendEmail(Arg.Any<PrivilegedControlsSeatLimitReachedMail>());
    }

    private void SetSeatsInUse(OrganizationEntity organization, int seatsInUse)
    {
        _organizationUserRepository.GetOccupiedPamSeatCountByOrganizationIdAsync(organization.Id).Returns(seatsInUse);
        _organizationUserRepository.GetManyByMinimumRoleAsync(organization.Id, OrganizationUserType.Owner)
            .Returns(new[] { new OrganizationUserUserDetails { Email = _ownerEmail } });
    }

    private OrganizationSubscriptionChangeSet SucceedWith()
    {
        var changeSet = new OrganizationSubscriptionChangeSet
        {
            Changes = [new UpdateItemQuantity("privileged-controls-enterprise-seat-annually", 15)]
        };
        _seatChangeSetFactory.CreateAsync(Arg.Any<OrganizationEntity>(), Arg.Any<int>()).Returns(changeSet);
        _updateOrganizationSubscriptionCommand
            .Run(Arg.Any<OrganizationEntity>(), changeSet, Arg.Any<Subscription?>())
            .Returns(new Subscription());
        return changeSet;
    }

    private static OrganizationEntity CreateOrganization(
        bool usePam = true,
        int? pamSeats = _boughtSeats,
        int? maxAutoscalePamSeats = null,
        string? gatewaySubscriptionId = "sub_123") => new()
        {
            Id = Guid.NewGuid(),
            Name = "Acme.Corp",
            PlanType = PlanType.EnterpriseAnnually,
            UsePam = usePam,
            PamSeats = pamSeats,
            MaxAutoscalePamSeats = maxAutoscalePamSeats,
            GatewayCustomerId = "cus_123",
            GatewaySubscriptionId = gatewaySubscriptionId
        };
}
