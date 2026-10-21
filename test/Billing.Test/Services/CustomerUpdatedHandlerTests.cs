using Bit.Billing.Services;
using Bit.Billing.Services.Implementations;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Stripe;
using Xunit;

namespace Bit.Billing.Test.Services;

public class CustomerUpdatedHandlerTests
{
    private readonly IOrganizationRepository _organizationRepository = Substitute.For<IOrganizationRepository>();
    private readonly IOrganizationService _organizationService = Substitute.For<IOrganizationService>();
    private readonly IStripeEventService _stripeEventService = Substitute.For<IStripeEventService>();
    private readonly IStripeEventUtilityService _stripeEventUtilityService = Substitute.For<IStripeEventUtilityService>();
    private readonly CustomerUpdatedHandler _sut;

    public CustomerUpdatedHandlerTests()
    {
        _sut = new CustomerUpdatedHandler(
            _organizationRepository,
            _organizationService,
            _stripeEventService,
            _stripeEventUtilityService,
            Substitute.For<ILogger<CustomerUpdatedHandler>>());
    }

    [Fact]
    public async Task HandleAsync_OrganizationCustomer_UpdatesBillingEmailAndCache()
    {
        // Arrange
        var organization = new Organization { Id = Guid.NewGuid(), BillingEmail = "old@example.com" };
        var metadata = new Dictionary<string, string> { ["organizationId"] = organization.Id.ToString() };
        var customer = new Customer
        {
            Id = "cus_test",
            Email = "new@example.com",
            Subscriptions = new StripeList<Subscription>
            {
                Data = [new Subscription { Id = "sub_test", Metadata = metadata }]
            }
        };
        var stripeEvent = new Event();

        _stripeEventService.GetCustomer(stripeEvent, true, Arg.Any<List<string>>()).Returns(customer);
        _stripeEventUtilityService.GetIdsFromMetadata(metadata)
            .Returns(new Tuple<Guid?, Guid?, Guid?>(organization.Id, null, null));
        _organizationRepository.GetByIdAsync(organization.Id).Returns(organization);

        // Act
        await _sut.HandleAsync(stripeEvent);

        // Assert
        await _organizationService.Received(1).ReplaceAndUpdateCacheAsync(Arg.Is<Organization>(o =>
            o.Id == organization.Id && o.BillingEmail == "new@example.com"));
    }
}
