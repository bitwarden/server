using Bit.Core.AdminConsole.Entities;
using Bit.Core.Billing.Constants;
using Bit.Core.Models.Business;

namespace Bit.Core.Billing.Models.Sales;

#nullable enable

public class OrganizationSale
{
    private OrganizationSale() { }

    public void Deconstruct(
        out Organization organization,
        out CustomerSetup? customerSetup,
        out SubscriptionSetup subscriptionSetup)
    {
        organization = Organization;
        customerSetup = CustomerSetup;
        subscriptionSetup = SubscriptionSetup;
    }

    public required Organization Organization { get; init; }
    public CustomerSetup? CustomerSetup { get; init; }
    public required SubscriptionSetup SubscriptionSetup { get; init; }

    public static OrganizationSale From(
        Organization organization,
        OrganizationSignup signup) => new()
        {
            Organization = organization,
            CustomerSetup = string.IsNullOrEmpty(organization.GatewayCustomerId) ? GetCustomerSetup(signup) : null,
            SubscriptionSetup = GetSubscriptionSetup(signup)
        };

    public static OrganizationSale From(
        Organization organization,
        OrganizationUpgrade upgrade) => new()
        {
            Organization = organization,
            SubscriptionSetup = GetSubscriptionSetup(upgrade)
        };

    private static CustomerSetup GetCustomerSetup(OrganizationSignup signup)
    {
        var customerSetup = new CustomerSetup
        {
            Coupon = signup.IsFromProvider
            ? StripeConstants.CouponIDs.MSPDiscount35
            : signup.IsFromSecretsManagerTrial
                ? StripeConstants.CouponIDs.SecretsManagerStandalone
                : null
        };

        if (!signup.PaymentMethodType.HasValue)
        {
            return customerSetup;
        }

        customerSetup.TokenizedPaymentSource = new TokenizedPaymentSource(
            signup.PaymentMethodType!.Value,
            signup.PaymentToken);

        customerSetup.TaxInformation = new TaxInformation(
            signup.TaxInfo.BillingAddressCountry,
            signup.TaxInfo.BillingAddressPostalCode,
            signup.TaxInfo.TaxIdNumber,
            signup.TaxInfo.BillingAddressLine1,
            signup.TaxInfo.BillingAddressLine2,
            signup.TaxInfo.BillingAddressCity,
            signup.TaxInfo.BillingAddressState);

        return customerSetup;
    }

    private static SubscriptionSetup GetSubscriptionSetup(OrganizationUpgrade upgrade)
    {
        var plan = Core.Utilities.StaticStore.GetPlan(upgrade.Plan);

        var passwordManagerOptions = new SubscriptionSetup.PasswordManager
        {
            Seats = upgrade.AdditionalSeats,
            Storage = upgrade.AdditionalStorageGb,
            PremiumAccess = upgrade.PremiumAccessAddon
        };

        var secretsManagerOptions = upgrade.UseSecretsManager
            ? new SubscriptionSetup.SecretsManager
            {
                Seats = upgrade.AdditionalSmSeats ?? 0,
                ServiceAccounts = upgrade.AdditionalServiceAccounts
            }
            : null;

        return new SubscriptionSetup
        {
            Plan = plan,
            PasswordManagerOptions = passwordManagerOptions,
            SecretsManagerOptions = secretsManagerOptions
        };
    }
}
