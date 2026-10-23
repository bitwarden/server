using Bit.Core.Billing.Enums;
using Bit.Core.Billing.Organizations.Commands;
using Bit.Core.Billing.Organizations.Helpers;
using Bit.Core.Billing.Organizations.Models;
using Bit.Core.Exceptions;
using Bit.Core.Services;
using Bit.Subscriptions.Organization.Commands;
using Bit.Subscriptions.Organization.Queries;
using Bitwarden.Server.Sdk.Environment;
using NSubstitute;
using Stripe;
using Xunit;
using OrganizationEntity = Bit.Core.AdminConsole.Entities.Organization;

namespace Bit.Subscriptions.Organization.Test.Commands;

public class RevertPrivilegedControlsSeatsCommandTests
{
    private readonly IPrivilegedControlsSeatChangeSetFactory _seatChangeSetFactory =
        Substitute.For<IPrivilegedControlsSeatChangeSetFactory>();
    private readonly IUpdateOrganizationSubscriptionCommand _updateOrganizationSubscriptionCommand =
        Substitute.For<IUpdateOrganizationSubscriptionCommand>();
    private readonly IOrganizationService _organizationService = Substitute.For<IOrganizationService>();
    private readonly IIsOnPrivilegedControlsSalesInitiatedTrialQuery _isOnTrialQuery =
        Substitute.For<IIsOnPrivilegedControlsSalesInitiatedTrialQuery>();
    private readonly IBitwardenEnvironment _environment = Substitute.For<IBitwardenEnvironment>();
    private readonly RevertPrivilegedControlsSeatsCommand _sut;

    public RevertPrivilegedControlsSeatsCommandTests() =>
        _sut = new RevertPrivilegedControlsSeatsCommand(
            _seatChangeSetFactory,
            _updateOrganizationSubscriptionCommand,
            _organizationService,
            _isOnTrialQuery,
            _environment);

    [Fact]
    public async Task Run_SeatsUnchanged_DoesNothing()
    {
        var organization = CreateOrganization(pamSeats: 5);

        await _sut.Run(organization, 5);

        await _seatChangeSetFactory.DidNotReceive().CreateAsync(Arg.Any<OrganizationEntity>(), Arg.Any<int>());
        await _updateOrganizationSubscriptionCommand.DidNotReceive()
            .Run(Arg.Any<OrganizationEntity>(), Arg.Any<OrganizationSubscriptionChangeSet>(), Arg.Any<Subscription?>());
        await _organizationService.DidNotReceive().ReplaceAndUpdateCacheAsync(Arg.Any<OrganizationEntity>());
    }

    [Fact]
    public async Task Run_OnTrial_ResetsSeatsOnOrganizationOnly()
    {
        var organization = CreateOrganization(pamSeats: 7);
        _isOnTrialQuery.Run(organization).Returns(true);

        await _sut.Run(organization, 5);

        Assert.Equal(5, organization.PamSeats);
        await _organizationService.Received(1).ReplaceAndUpdateCacheAsync(organization);
        await _seatChangeSetFactory.DidNotReceive().CreateAsync(Arg.Any<OrganizationEntity>(), Arg.Any<int>());
        await _updateOrganizationSubscriptionCommand.DidNotReceive()
            .Run(Arg.Any<OrganizationEntity>(), Arg.Any<OrganizationSubscriptionChangeSet>(), Arg.Any<Subscription?>());
    }

    [Fact]
    public async Task Run_Paid_UpdatesSubscriptionThenSaves()
    {
        var organization = CreateOrganization(pamSeats: 7);
        _isOnTrialQuery.Run(organization).Returns(false);
        var changeSet = new OrganizationSubscriptionChangeSet
        {
            Changes = [new UpdateItemQuantity("privileged-controls-enterprise-seat-annually", 5)]
        };
        _seatChangeSetFactory.CreateAsync(organization, 5).Returns(changeSet);
        _updateOrganizationSubscriptionCommand
            .Run(organization, changeSet, Arg.Any<Subscription?>())
            .Returns(new Subscription());

        await _sut.Run(organization, 5);

        await _seatChangeSetFactory.Received(1).CreateAsync(organization, 5);
        await _updateOrganizationSubscriptionCommand.Received(1).Run(organization, changeSet);
        Assert.Equal(5, organization.PamSeats);
        await _organizationService.Received(1).ReplaceAndUpdateCacheAsync(organization);
    }

    [Fact]
    public async Task Run_SelfHosted_ThrowsBadRequest()
    {
        var organization = CreateOrganization(pamSeats: 7);
        _environment.SelfHosted.Returns(true);

        await Assert.ThrowsAsync<BadRequestException>(() => _sut.Run(organization, 5));
    }

    private static OrganizationEntity CreateOrganization(int pamSeats) => new()
    {
        Id = Guid.NewGuid(),
        Name = "Acme.Corp",
        PlanType = PlanType.EnterpriseAnnually,
        UsePam = true,
        PamSeats = pamSeats,
        PamSeatMinimum = 10,
        GatewayCustomerId = "cus_123",
        GatewaySubscriptionId = "sub_123"
    };
}
