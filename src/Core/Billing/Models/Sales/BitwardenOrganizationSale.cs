using Bit.Core.AdminConsole.Entities;
using Bit.Core.Billing.Constants;
using Bit.Core.Models.Business;

namespace Bit.Core.Billing.Models.Sales;

#nullable enable

public class BitwardenOrganizationSale
{
    private BitwardenOrganizationSale() {}

    public void Deconstruct(
        out Organization organization,
        out CustomerSetup? customerSetup,
        out SubscriptionSetup subscriptionSetup)
    {
        organization = Organization;
        customerSetup = CustomerSetup;
        subscriptionSetup = SubscriptionSetup;
    }

    public required Organization Organization { get; set; }
    public CustomerSetup? CustomerSetup { get; set; }
    public required SubscriptionSetup SubscriptionSetup { get; set; }

    public static BitwardenOrganizationSale From(
        Organization organization,
        OrganizationSignup signup) => new ()
    {
        Organization = organization,
        CustomerSetup = string.IsNullOrEmpty(organization.GatewayCustomerId) ? GetCustomerSetup(signup) : null,
        SubscriptionSetup = GetSubscriptionSetup(signup)
    };

    private static CustomerSetup? GetCustomerSetup(OrganizationSignup signup)
    {
        if (!signup.PaymentMethodType.HasValue)
        {
            return null;
        }

        var tokenizedPaymentSource = new TokenizedPaymentSource(
            signup.PaymentMethodType!.Value,
            signup.PaymentToken);

        var taxInformation = new TaxInformation(
            signup.TaxInfo.BillingAddressCountry,
            signup.TaxInfo.BillingAddressPostalCode,
            signup.TaxInfo.TaxIdNumber,
            signup.TaxInfo.BillingAddressLine1,
            signup.TaxInfo.BillingAddressLine2,
            signup.TaxInfo.BillingAddressCity,
            signup.TaxInfo.BillingAddressState);

        var coupon = signup.IsFromProvider
            ? StripeConstants.CouponIDs.MSPDiscount35
            : signup.IsFromSecretsManagerTrial
                ? StripeConstants.CouponIDs.SecretsManagerStandalone
                : null;

        return new CustomerSetup
        {
            TokenizedPaymentSource = tokenizedPaymentSource, TaxInformation = taxInformation, Coupon = coupon
        };
    }

    private static SubscriptionSetup GetSubscriptionSetup(OrganizationSignup signup)
    {
        var plan = Core.Utilities.StaticStore.GetPlan(signup.Plan);

        var passwordManagerOptions = new SubscriptionSetup.PasswordManager
        {
            Seats = signup.AdditionalSeats,
            Storage = signup.AdditionalStorageGb,
            PremiumAccess = signup.PremiumAccessAddon
        };

        var secretsManagerOptions = signup.UseSecretsManager
            ? new SubscriptionSetup.SecretsManager
            {
                Seats = signup.AdditionalSmSeats ?? 0, ServiceAccounts = signup.AdditionalServiceAccounts
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
