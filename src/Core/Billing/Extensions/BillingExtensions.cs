using System.Reflection;
using System.Text;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Entities.Provider;
using Bit.Core.AdminConsole.Enums.Provider;
using Bit.Core.Billing.Attributes;
using Bit.Core.Billing.Enums;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Models.Business;
using Bit.Core.Utilities;
using Stripe;

namespace Bit.Core.Billing.Extensions;

public static class BillingExtensions
{
    public static bool IsBillable(this Provider provider) =>
        provider is
        {
            Type: ProviderType.Msp,
            Status: ProviderStatusType.Billable
        };

    public static bool IsValidClient(this Organization organization)
        => organization is
        {
            Seats: not null,
            Status: OrganizationStatusType.Managed,
            PlanType: PlanType.TeamsMonthly or PlanType.EnterpriseMonthly
        };

    public static bool IsStripeEnabled(this ISubscriber subscriber)
        => !string.IsNullOrEmpty(subscriber.GatewayCustomerId) &&
           !string.IsNullOrEmpty(subscriber.GatewaySubscriptionId);

    public static bool IsUnverifiedBankAccount(this SetupIntent setupIntent) =>
        setupIntent is
        {
            Status: "requires_action",
            NextAction:
            {
                VerifyWithMicrodeposits: not null
            },
            PaymentMethod:
            {
                UsBankAccount: not null
            }
        };

    public static bool SupportsConsolidatedBilling(this PlanType planType)
        => planType is PlanType.TeamsMonthly or PlanType.EnterpriseMonthly;

    public static bool ShouldIncludePropertyOnLicense(
        this PropertyInfo property,
        int licenseVersion,
        LicenseIgnoreCondition additionalCondition = LicenseIgnoreCondition.Always)
    {
        var licenseIgnoreAttribute = property.GetCustomAttribute<LicenseIgnoreAttribute>();
        var shouldNotIgnore = licenseIgnoreAttribute is null ||
                              licenseIgnoreAttribute.Condition != LicenseIgnoreCondition.Always &&
                              licenseIgnoreAttribute.Condition != additionalCondition;
        var versionIsSupported = (property.GetCustomAttribute<LicenseVersionAttribute>()?.Version ?? 1) <= licenseVersion;

        return shouldNotIgnore && versionIsSupported;
    }

    public static byte[] EncodeLicense(this ILicense license, Func<PropertyInfo, bool> shouldIncludeProperty)
    {
        var prefix = license switch
        {
            UserLicense => "license:user",
            OrganizationLicense => "license:organization",
            _ => throw new NotSupportedException()
        };

        var props = license.GetType()
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(shouldIncludeProperty)
            .OrderBy(p => p.Name)
            .Select(p => $"{p.Name}:{CoreHelpers.FormatLicenseSignatureValue(p.GetValue(license))}")
            .Aggregate((c, n) => $"{c}|{n}");

        var data = $"{prefix}|{props}";

        return Encoding.UTF8.GetBytes(data);
    }
}
