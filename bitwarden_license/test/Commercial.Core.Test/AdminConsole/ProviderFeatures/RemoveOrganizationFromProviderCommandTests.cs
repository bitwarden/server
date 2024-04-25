using Bit.Commercial.Core.AdminConsole.Providers;
using Bit.Core;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Entities.Provider;
using Bit.Core.AdminConsole.Enums.Provider;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Billing.Commands;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Core.Services;
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

        sutProvider.GetDependency<IOrganizationService>().HasConfirmedOwnersExceptAsync(
            providerOrganization.OrganizationId,
            Array.Empty<Guid>(),
            includeProvider: false)
            .Returns(false);

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.RemoveOrganizationFromProvider(provider, providerOrganization, organization));

        Assert.Equal("Organization must have at least one confirmed owner.", exception.Message);
    }

    [Theory, BitAutoData]
    public async Task RemoveOrganizationFromProvider_MakesCorrectInvocations(
        Provider provider,
        ProviderOrganization providerOrganization,
        Organization organization,
        SutProvider<RemoveOrganizationFromProviderCommand> sutProvider)
    {
        providerOrganization.ProviderId = provider.Id;

        var organizationRepository = sutProvider.GetDependency<IOrganizationRepository>();

        sutProvider.GetDependency<IOrganizationService>().HasConfirmedOwnersExceptAsync(
                providerOrganization.OrganizationId,
                Array.Empty<Guid>(),
                includeProvider: false)
            .Returns(true);

        var organizationOwnerEmails = new List<string> { "a@example.com", "b@example.com" };

        organizationRepository.GetOwnerEmailAddressesById(organization.Id).Returns(organizationOwnerEmails);
        var stripeAdapter = sutProvider.GetDependency<IStripeAdapter>();
        stripeAdapter.SubscriptionCreateAsync(default).ReturnsForAnyArgs(new Stripe.Subscription
        {
            Id = "S-1",
            CurrentPeriodEnd = DateTime.Today.AddDays(10),
        });

        await sutProvider.Sut.RemoveOrganizationFromProvider(provider, providerOrganization, organization);

        await organizationRepository.Received(1).ReplaceAsync(Arg.Is<Organization>(
            org => org.Id == organization.Id && org.BillingEmail == "a@example.com"));

        await stripeAdapter.Received(1).CustomerUpdateAsync(
            organization.GatewayCustomerId, Arg.Is<CustomerUpdateOptions>(
                options => options.Coupon == string.Empty && options.Email == "a@example.com"));

        await sutProvider.GetDependency<IMailService>().Received(1).SendProviderUpdatePaymentMethod(
            organization.Id,
            organization.Name,
            provider.Name,
            Arg.Is<IEnumerable<string>>(emails => emails.Contains("a@example.com") && emails.Contains("b@example.com")));

        await sutProvider.GetDependency<IProviderOrganizationRepository>().Received(1)
            .DeleteAsync(providerOrganization);

        await sutProvider.GetDependency<IEventService>().Received(1).LogProviderOrganizationEventAsync(
            providerOrganization,
            EventType.ProviderOrganization_Removed);
    }

    [Theory, BitAutoData]
    public async Task RemoveOrganizationFromProvider_CreatesSubscriptionAndScalesSeats_FeatureFlagON(Provider provider,
        ProviderOrganization providerOrganization,
        Organization organization,
        SutProvider<RemoveOrganizationFromProviderCommand> sutProvider)
    {
        providerOrganization.ProviderId = provider.Id;
        provider.Status = ProviderStatusType.Billable;
        var organizationRepository = sutProvider.GetDependency<IOrganizationRepository>();
        sutProvider.GetDependency<IOrganizationService>().HasConfirmedOwnersExceptAsync(
                providerOrganization.OrganizationId,
                Array.Empty<Guid>(),
                includeProvider: false)
            .Returns(true);

        var organizationOwnerEmails = new List<string> { "a@example.com", "b@example.com" };

        organizationRepository.GetOwnerEmailAddressesById(organization.Id).Returns(organizationOwnerEmails);

        var stripeAdapter = sutProvider.GetDependency<IStripeAdapter>();
        stripeAdapter.SubscriptionCreateAsync(default).ReturnsForAnyArgs(new Stripe.Subscription
        {
            Id = "S-1",
            CurrentPeriodEnd = DateTime.Today.AddDays(10),
        });
        sutProvider.GetDependency<IFeatureService>().IsEnabled(FeatureFlagKeys.EnableConsolidatedBilling).Returns(true);
        await sutProvider.Sut.RemoveOrganizationFromProvider(provider, providerOrganization, organization);
        await stripeAdapter.Received(1).CustomerUpdateAsync(
            organization.GatewayCustomerId, Arg.Is<CustomerUpdateOptions>(
                options => options.Coupon == string.Empty && options.Email == "a@example.com"));

        await stripeAdapter.Received(1).SubscriptionCreateAsync(Arg.Is<SubscriptionCreateOptions>(c =>
            c.Customer == organization.GatewayCustomerId &&
            c.CollectionMethod == "send_invoice" &&
            c.DaysUntilDue == 30 &&
            c.Items.Count == 1
        ));

        await sutProvider.GetDependency<IScaleSeatsCommand>().Received(1)
            .ScalePasswordManagerSeats(provider, organization.PlanType, -(int)organization.Seats);

        await organizationRepository.Received(1).ReplaceAsync(Arg.Is<Organization>(
            org => org.Id == organization.Id && org.BillingEmail == "a@example.com" &&
                   org.GatewaySubscriptionId == "S-1"));

        await sutProvider.GetDependency<IMailService>().Received(1).SendProviderUpdatePaymentMethod(
            organization.Id,
            organization.Name,
            provider.Name,
            Arg.Is<IEnumerable<string>>(emails =>
                emails.Contains("a@example.com") && emails.Contains("b@example.com")));

        await sutProvider.GetDependency<IProviderOrganizationRepository>().Received(1)
            .DeleteAsync(providerOrganization);

        await sutProvider.GetDependency<IEventService>().Received(1).LogProviderOrganizationEventAsync(
            providerOrganization,
            EventType.ProviderOrganization_Removed);
    }
}
