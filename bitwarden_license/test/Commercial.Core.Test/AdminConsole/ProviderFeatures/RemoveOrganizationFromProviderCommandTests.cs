using Bit.Commercial.Core.AdminConsole.Providers;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Entities.Provider;
using Bit.Core.AdminConsole.Enums.Provider;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Interfaces;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Billing.Constants;
using Bit.Core.Billing.Enums;
using Bit.Core.Billing.Services;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Utilities;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Stripe;
using Xunit;

namespace Bit.Commercial.Core.Test.AdminConsole.ProviderFeatures;

[SutProviderCustomize]
public class RemoveOrganizationFromProviderCommandTests
{
    [Theory, BitAutoData]
    public async Task RemoveOrganizationFromProvider_NoProvider_BadRequest(
        SutProvider<RemoveOrganizationFromProviderCommand> sutProvider)
    {
        var exception = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.RemoveOrganizationFromProvider(null, null, null));

        Assert.Equal("Failed to remove organization. Please contact support.", exception.Message);
    }

    [Theory, BitAutoData]
    public async Task RemoveOrganizationFromProvider_NoProviderOrganization_BadRequest(
        Provider provider,
        SutProvider<RemoveOrganizationFromProviderCommand> sutProvider)
    {
        var exception = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.RemoveOrganizationFromProvider(provider, null, null));

        Assert.Equal("Failed to remove organization. Please contact support.", exception.Message);
    }

    [Theory, BitAutoData]
    public async Task RemoveOrganizationFromProvider_NoOrganization_BadRequest(
        Provider provider,
        ProviderOrganization providerOrganization,
        SutProvider<RemoveOrganizationFromProviderCommand> sutProvider)
    {
        var exception = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.RemoveOrganizationFromProvider(
            provider, providerOrganization, null));

        Assert.Equal("Failed to remove organization. Please contact support.", exception.Message);
    }

    [Theory, BitAutoData]
    public async Task RemoveOrganizationFromProvider_MismatchedProviderOrganization_BadRequest(
        Provider provider,
        ProviderOrganization providerOrganization,
        Organization organization,
        SutProvider<RemoveOrganizationFromProviderCommand> sutProvider)
    {
        var exception = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.RemoveOrganizationFromProvider(provider, providerOrganization, organization));

        Assert.Equal("Failed to remove organization. Please contact support.", exception.Message);
    }

    [Theory, BitAutoData]
    public async Task RemoveOrganizationFromProvider_NoConfirmedOwners_BadRequest(
        Provider provider,
        ProviderOrganization providerOrganization,
        Organization organization,
        SutProvider<RemoveOrganizationFromProviderCommand> sutProvider)
    {
        providerOrganization.ProviderId = provider.Id;

        sutProvider.GetDependency<IHasConfirmedOwnersExceptQuery>().HasConfirmedOwnersExceptAsync(
                providerOrganization.OrganizationId,
                [],
                includeProvider: false)
            .Returns(false);

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.RemoveOrganizationFromProvider(provider, providerOrganization, organization));

        Assert.Equal("Organization must have at least one confirmed owner.", exception.Message);
    }

    [Theory, BitAutoData]
    public async Task RemoveOrganizationFromProvider_OrganizationNotStripeEnabled_MakesCorrectInvocations(
        Provider provider,
        ProviderOrganization providerOrganization,
        Organization organization,
        SutProvider<RemoveOrganizationFromProviderCommand> sutProvider)
    {
        providerOrganization.ProviderId = provider.Id;

        organization.GatewayCustomerId = null;
        organization.GatewaySubscriptionId = null;

        sutProvider.GetDependency<IHasConfirmedOwnersExceptQuery>().HasConfirmedOwnersExceptAsync(
                providerOrganization.OrganizationId,
                [],
                includeProvider: false)
            .Returns(true);

        var organizationRepository = sutProvider.GetDependency<IOrganizationRepository>();

        organizationRepository.GetOwnerEmailAddressesById(organization.Id).Returns([
            "a@example.com",
            "b@example.com"
        ]);

        await sutProvider.Sut.RemoveOrganizationFromProvider(provider, providerOrganization, organization);

        await organizationRepository.Received(1).ReplaceAsync(Arg.Is<Organization>(org => org.BillingEmail == "a@example.com"));

        await sutProvider.GetDependency<IProviderOrganizationRepository>().Received(1)
            .DeleteAsync(providerOrganization);

        await sutProvider.GetDependency<IEventService>().Received(1)
            .LogProviderOrganizationEventAsync(providerOrganization, EventType.ProviderOrganization_Removed);

        await sutProvider.GetDependency<IMailService>().Received(1)
            .SendProviderUpdatePaymentMethod(
                organization.Id,
                organization.Name,
                provider.Name,
                Arg.Is<IEnumerable<string>>(emails => emails.FirstOrDefault() == "a@example.com"));

        await sutProvider.GetDependency<IStripeAdapter>().DidNotReceiveWithAnyArgs()
            .CustomerUpdateAsync(Arg.Any<string>(), Arg.Any<CustomerUpdateOptions>());
    }

    [Theory, BitAutoData]
    public async Task RemoveOrganizationFromProvider_OrganizationStripeEnabled_NonConsolidatedBilling_MakesCorrectInvocations(
        Provider provider,
        ProviderOrganization providerOrganization,
        Organization organization,
        SutProvider<RemoveOrganizationFromProviderCommand> sutProvider)
    {
        providerOrganization.ProviderId = provider.Id;

        sutProvider.GetDependency<IHasConfirmedOwnersExceptQuery>().HasConfirmedOwnersExceptAsync(
                providerOrganization.OrganizationId,
                [],
                includeProvider: false)
            .Returns(true);

        var organizationRepository = sutProvider.GetDependency<IOrganizationRepository>();

        organizationRepository.GetOwnerEmailAddressesById(organization.Id).Returns([
            "a@example.com",
            "b@example.com"
        ]);

        sutProvider.GetDependency<IStripeAdapter>().SubscriptionGetAsync(organization.GatewaySubscriptionId)
            .Returns(GetSubscription(organization.GatewaySubscriptionId));

        await sutProvider.Sut.RemoveOrganizationFromProvider(provider, providerOrganization, organization);

        var stripeAdapter = sutProvider.GetDependency<IStripeAdapter>();

        await stripeAdapter.Received(1).CustomerUpdateAsync(organization.GatewayCustomerId,
            Arg.Is<CustomerUpdateOptions>(options =>
                options.Coupon == string.Empty && options.Email == "a@example.com"));

        await stripeAdapter.Received(1).SubscriptionUpdateAsync(organization.GatewaySubscriptionId,
            Arg.Is<SubscriptionUpdateOptions>(options =>
                options.CollectionMethod == StripeConstants.CollectionMethod.SendInvoice &&
                options.DaysUntilDue == 30));

        await sutProvider.GetDependency<ISubscriberService>().Received(1).RemovePaymentSource(organization);

        await organizationRepository.Received(1).ReplaceAsync(Arg.Is<Organization>(org => org.BillingEmail == "a@example.com"));

        await sutProvider.GetDependency<IProviderOrganizationRepository>().Received(1)
            .DeleteAsync(providerOrganization);

        await sutProvider.GetDependency<IEventService>().Received(1)
            .LogProviderOrganizationEventAsync(providerOrganization, EventType.ProviderOrganization_Removed);

        await sutProvider.GetDependency<IMailService>().Received(1)
            .SendProviderUpdatePaymentMethod(
                organization.Id,
                organization.Name,
                provider.Name,
                Arg.Is<IEnumerable<string>>(emails => emails.FirstOrDefault() == "a@example.com"));
    }

    [Theory, BitAutoData]
    public async Task RemoveOrganizationFromProvider_OrganizationStripeEnabled_ConsolidatedBilling_MakesCorrectInvocations(
        Provider provider,
        ProviderOrganization providerOrganization,
        Organization organization,
        SutProvider<RemoveOrganizationFromProviderCommand> sutProvider)
    {
        provider.Status = ProviderStatusType.Billable;

        providerOrganization.ProviderId = provider.Id;

        organization.Status = OrganizationStatusType.Managed;

        organization.PlanType = PlanType.TeamsMonthly;

        var teamsMonthlyPlan = StaticStore.GetPlan(PlanType.TeamsMonthly);

        sutProvider.GetDependency<IHasConfirmedOwnersExceptQuery>().HasConfirmedOwnersExceptAsync(
                providerOrganization.OrganizationId,
                [],
                includeProvider: false)
            .Returns(true);

        var organizationRepository = sutProvider.GetDependency<IOrganizationRepository>();

        organizationRepository.GetOwnerEmailAddressesById(organization.Id).Returns([
            "a@example.com",
            "b@example.com"
        ]);

        var stripeAdapter = sutProvider.GetDependency<IStripeAdapter>();

        stripeAdapter.SubscriptionCreateAsync(Arg.Any<SubscriptionCreateOptions>()).Returns(new Subscription
        {
            Id = "subscription_id"
        });

        await sutProvider.Sut.RemoveOrganizationFromProvider(provider, providerOrganization, organization);

        await stripeAdapter.Received(1).SubscriptionCreateAsync(Arg.Is<SubscriptionCreateOptions>(options =>
            options.Customer == organization.GatewayCustomerId &&
            options.CollectionMethod == StripeConstants.CollectionMethod.SendInvoice &&
            options.DaysUntilDue == 30 &&
            options.AutomaticTax.Enabled == true &&
            options.Metadata["organizationId"] == organization.Id.ToString() &&
            options.OffSession == true &&
            options.ProrationBehavior == StripeConstants.ProrationBehavior.CreateProrations &&
            options.Items.First().Price == teamsMonthlyPlan.PasswordManager.StripeSeatPlanId &&
            options.Items.First().Quantity == organization.Seats));

        await sutProvider.GetDependency<IProviderBillingService>().Received(1)
            .ScaleSeats(provider, organization.PlanType, -organization.Seats ?? 0);

        await organizationRepository.Received(1).ReplaceAsync(Arg.Is<Organization>(
            org =>
                org.BillingEmail == "a@example.com" &&
                org.GatewaySubscriptionId == "subscription_id" &&
                org.Status == OrganizationStatusType.Created));

        await sutProvider.GetDependency<IProviderOrganizationRepository>().Received(1)
            .DeleteAsync(providerOrganization);

        await sutProvider.GetDependency<IEventService>().Received(1)
            .LogProviderOrganizationEventAsync(providerOrganization, EventType.ProviderOrganization_Removed);

        await sutProvider.GetDependency<IMailService>().Received(1)
            .SendProviderUpdatePaymentMethod(
                organization.Id,
                organization.Name,
                provider.Name,
                Arg.Is<IEnumerable<string>>(emails => emails.FirstOrDefault() == "a@example.com"));
    }

    private static Subscription GetSubscription(string subscriptionId) =>
        new()
        {
            Id = subscriptionId,
            Status = StripeConstants.SubscriptionStatus.Active,
            Items = new StripeList<SubscriptionItem>
            {
                Data = new List<SubscriptionItem>
                {
                    new()
                    {
                        Id = "sub_item_123",
                        Price = new Price()
                        {
                            Id = "2023-enterprise-org-seat-annually"
                        }
                    }
                }
            }
        };
}
