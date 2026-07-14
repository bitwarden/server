using Bit.Test.Common.Helpers;

namespace Bit.Billing.IntegrationTest;

public class PreExistingStateTests(StripeTestsFixture fixture) : IClassFixture<StripeTestsFixture>
{
    [BillingFact]
    public async Task RemoveOrganizationFromProvider_FetchesCustomerWithInvoiceSettingsAndSources()
    {
        // PrepareProviderAdminAsync converts an org to a provider-managed org, so removing
        // *any* org from that provider lands on the managed organization we just created.
        var (_, providerId) = await fixture.PrepareProviderAdminAsync(
            "remove-org-from-provider@example.com");

        // Drives RemoveOrganizationFromProviderCommand -> ISubscriberService.RemovePaymentSource
        // on the organization, which calls GetCustomerOrThrow with
        // Expand=["invoice_settings.default_payment_method", "sources"].
        await fixture.RemoveAnyOrganizationFromProviderAsync(providerId);
    }

    [BillingFact]
    public async Task RemoveOrganizationFromProvider_WhenCustomerHasActiveDiscount_DeletesTheDiscount()
    {
        // Adopt a Stripe-enabled org (own subscription, non-Managed status) into a reseller provider so
        // removal takes the `else if (organization.IsStripeEnabled())` branch. That branch reads
        // subscription.Customer.Discount.Source.Coupon and deletes it — requiring
        // `customer.discount.source.coupon` in the fetch expand (the 2025-09-30.clover Source.Coupon
        // refactor). With only `customer` expanded, Coupon is a null stub and the deletion no-ops.
        var (_, _, organizationId, _) =
            await fixture.PrepareOrganizationOwnerAsync("remove-org-discount@example.com");
        var customerId = await fixture.GetOrganizationGatewayCustomerIdAsync(organizationId);
        var providerId = await fixture.AdoptOrganizationIntoResellerProviderAsync(organizationId);

        await fixture.SeedAndAttachCustomerCouponAsync(customerId, $"rm_prov_{Guid.NewGuid():N}", percentOff: 20);
        Assert.True(await fixture.CustomerHasDiscountAsync(customerId), "precondition: coupon should be attached");

        await fixture.RemoveAnyOrganizationFromProviderAsync(providerId);

        Assert.False(await fixture.CustomerHasDiscountAsync(customerId));
    }

    [BillingFact]
    public async Task ProviderPaymentSource_WhenCustomerHasNoDefaultPM_ListsSetupIntentsWithPaymentMethodExpand()
    {
        var (client, providerId) = await fixture.PrepareProviderAdminAsync(
            "provider-no-default-pm@example.com");
        var customerId = await fixture.GetProviderGatewayCustomerIdAsync(providerId);
        await fixture.DetachDefaultPaymentMethodAsync(customerId);

        // Drives subscriberService.GetPaymentSource -> GetPaymentSourceAsync -> the
        // setup-intents listing with Expand=["data.payment_method"].
        var response = await client.GetAsync($"/providers/{providerId}/billing/subscription");
        await Assert.SuccessResponseAsync(response);
    }
}
