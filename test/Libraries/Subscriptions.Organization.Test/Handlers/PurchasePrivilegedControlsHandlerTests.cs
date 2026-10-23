using Bit.Core.Billing.Enums;
using Bit.Core.Billing.Pricing;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Subscriptions.Organization.Commands;
using Bit.Subscriptions.Organization.Handlers;
using Bit.Subscriptions.Organization.Models.Requests;
using Bit.Subscriptions.Organization.Queries;
using NSubstitute;
using Xunit;
using OrganizationEntity = Bit.Core.AdminConsole.Entities.Organization;
using PlanFeatures = Bit.Core.Models.StaticStore.Plan;

namespace Bit.Subscriptions.Organization.Test.Handlers;

public class PurchasePrivilegedControlsHandlerTests
{
    private const string _representativeMessage =
        "Work with your customer service representative to purchase Privileged Controls.";

    private readonly IOrganizationRepository _organizationRepository = Substitute.For<IOrganizationRepository>();
    private readonly IPricingClient _pricingClient = Substitute.For<IPricingClient>();
    private readonly FakeIsOnPrivilegedControlsSalesInitiatedTrialQuery _trialQuery = new();
    private readonly FakeAddPrivilegedControlsCommand _command = new();
    private readonly PurchasePrivilegedControlsHandler _sut;

    public PurchasePrivilegedControlsHandlerTests() =>
        _sut = new PurchasePrivilegedControlsHandler(
            _organizationRepository,
            _pricingClient,
            _trialQuery,
            _command);

    [Fact]
    public async Task HandleAsync_OrganizationMissing_ThrowsNotFoundWithoutCheckingTrialOrCallingCommand()
    {
        var organizationId = Guid.NewGuid();
        _organizationRepository.GetByIdAsync(organizationId).Returns((OrganizationEntity?)null);

        await Assert.ThrowsAsync<NotFoundException>(() => _sut.HandleAsync(organizationId, Request()));

        Assert.Null(_trialQuery.ReceivedOrganization);
        Assert.False(_command.WasCalled);
    }

    [Fact]
    public async Task HandleAsync_SalesInitiatedTrial_ThrowsBadRequestWithRepresentativeMessage()
    {
        var organization = Arrange(CreateOrganization());
        _trialQuery.Result = true;

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => _sut.HandleAsync(organization.Id, Request()));

        Assert.Equal(_representativeMessage, exception.Message);
        Assert.Same(organization, _trialQuery.ReceivedOrganization);
        Assert.False(_command.WasCalled);
    }

    [Fact]
    public async Task HandleAsync_SalesInitiatedTrialWithNoGatewaySubscriptionId_ThrowsRepresentativeMessageNotNoSubscription()
    {
        var organization = Arrange(CreateOrganization(gatewaySubscriptionId: null));
        _trialQuery.Result = true;

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => _sut.HandleAsync(organization.Id, Request()));

        Assert.Equal(_representativeMessage, exception.Message);
    }

    [Fact]
    public async Task HandleAsync_TrialQueryThrowsConflict_PropagatesWithoutCallingCommand()
    {
        var organization = Arrange(CreateOrganization());
        var thrown = new ConflictException(
            "Your organization's subscription is in an invalid state. Please contact support for assistance.");
        _trialQuery.ExceptionToThrow = thrown;

        var exception = await Assert.ThrowsAsync<ConflictException>(() => _sut.HandleAsync(organization.Id, Request()));

        Assert.Same(thrown, exception);
        Assert.False(_command.WasCalled);
    }

    [Fact]
    public async Task HandleAsync_PlanDoesNotSupportPrivilegedControls_ThrowsBadRequestWithoutCallingCommand()
    {
        var organization = CreateOrganization(planType: PlanType.TeamsAnnually);
        Arrange(organization, new TestPlan(ProductTierType.Teams, supportsPrivilegedControls: false));

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => _sut.HandleAsync(organization.Id, Request()));

        Assert.Equal("Privileged Controls is only available on Enterprise organizations.", exception.Message);
        Assert.False(_command.WasCalled);
    }

    [Fact]
    public async Task HandleAsync_NoGatewaySubscriptionId_ThrowsBadRequestNoSubscriptionFound()
    {
        var organization = Arrange(CreateOrganization(gatewaySubscriptionId: null));

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => _sut.HandleAsync(organization.Id, Request()));

        Assert.Equal("No subscription found.", exception.Message);
        Assert.False(_command.WasCalled);
    }

    [Fact]
    public async Task HandleAsync_ValidOrganization_CallsCommandWithSeatsAndAutoscaleLimit()
    {
        var organization = Arrange(CreateOrganization());

        await _sut.HandleAsync(organization.Id, Request(seats: 10, maxAutoscaleSeats: 20));

        Assert.Same(organization, _command.ReceivedOrganization);
        Assert.Equal(10, _command.ReceivedSeats);
        Assert.Equal(20, _command.ReceivedMaxAutoscaleSeats);
    }

    [Fact]
    public async Task HandleAsync_CommandThrowsBadRequest_PropagatesUnchanged()
    {
        var organization = Arrange(CreateOrganization());
        var thrown = new BadRequestException("Privileged Controls requires at least 10 seats.");
        _command.ExceptionToThrow = thrown;

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => _sut.HandleAsync(organization.Id, Request(seats: 3)));

        Assert.Same(thrown, exception);
    }

    private OrganizationEntity Arrange(OrganizationEntity organization, PlanFeatures? plan = null)
    {
        _organizationRepository.GetByIdAsync(organization.Id).Returns(organization);
        _pricingClient.GetPlanOrThrow(organization.PlanType)
            .Returns(plan ?? new TestPlan(ProductTierType.Enterprise, supportsPrivilegedControls: true));
        return organization;
    }

    private static OrganizationEntity CreateOrganization(
        PlanType planType = PlanType.EnterpriseAnnually,
        string? gatewaySubscriptionId = "sub_123") => new()
        {
            Id = Guid.NewGuid(),
            PlanType = planType,
            GatewayCustomerId = "cus_123",
            GatewaySubscriptionId = gatewaySubscriptionId
        };

    private static PurchasePrivilegedControlsRequest Request(int seats = 10, int? maxAutoscaleSeats = null) =>
        new() { Seats = seats, MaxAutoscaleSeats = maxAutoscaleSeats };

    private sealed record TestPlan : PlanFeatures
    {
        public TestPlan(ProductTierType productTier, bool supportsPrivilegedControls)
        {
            ProductTier = productTier;
            if (supportsPrivilegedControls)
            {
                PrivilegedControls = new PrivilegedControlsPlanFeatures
                {
                    StripeSeatPlanId = "privileged-controls-enterprise-seat-annually",
                    DefaultSeatMinimum = 10
                };
            }
        }
    }

    private sealed class FakeIsOnPrivilegedControlsSalesInitiatedTrialQuery : IIsOnPrivilegedControlsSalesInitiatedTrialQuery
    {
        public bool Result { get; set; }
        public Exception? ExceptionToThrow { get; set; }
        public OrganizationEntity? ReceivedOrganization { get; private set; }

        public Task<bool> Run(OrganizationEntity organization)
        {
            ReceivedOrganization = organization;
            return ExceptionToThrow is null ? Task.FromResult(Result) : Task.FromException<bool>(ExceptionToThrow);
        }
    }

    private sealed class FakeAddPrivilegedControlsCommand : IAddPrivilegedControlsToExistingSubscriptionCommand
    {
        public bool WasCalled { get; private set; }
        public OrganizationEntity? ReceivedOrganization { get; private set; }
        public int ReceivedSeats { get; private set; }
        public int? ReceivedMaxAutoscaleSeats { get; private set; }
        public Exception? ExceptionToThrow { get; set; }

        public Task Run(OrganizationEntity organization, int seats, int? maxAutoscaleSeats)
        {
            WasCalled = true;
            ReceivedOrganization = organization;
            ReceivedSeats = seats;
            ReceivedMaxAutoscaleSeats = maxAutoscaleSeats;
            return ExceptionToThrow is null ? Task.CompletedTask : Task.FromException(ExceptionToThrow);
        }
    }
}
