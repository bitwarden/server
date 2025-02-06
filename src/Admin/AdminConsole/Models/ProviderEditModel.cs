using System.ComponentModel.DataAnnotations;
using Bit.Core.AdminConsole.Entities.Provider;
using Bit.Core.AdminConsole.Enums.Provider;
using Bit.Core.AdminConsole.Models.Data.Provider;
using Bit.Core.Billing.Entities;
using Bit.Core.Billing.Enums;
using Bit.Core.Enums;
using Bit.SharedWeb.Utilities;

namespace Bit.Admin.AdminConsole.Models;

public class ProviderEditModel : ProviderViewModel, IValidatableObject
{
    public ProviderEditModel() { }

    public ProviderEditModel(
        Provider provider,
        IEnumerable<ProviderUserUserDetails> providerUsers,
        IEnumerable<ProviderOrganizationOrganizationDetails> organizations,
        IReadOnlyCollection<ProviderPlan> providerPlans,
        string gatewayCustomerUrl = null,
        string gatewaySubscriptionUrl = null) : base(provider, providerUsers, organizations)
    {
        Name = provider.DisplayName();
        BusinessName = provider.DisplayBusinessName();
        BillingEmail = provider.BillingEmail;
        BillingPhone = provider.BillingPhone;
        TeamsMonthlySeatMinimum = GetSeatMinimum(providerPlans, PlanType.TeamsMonthly);
        EnterpriseMonthlySeatMinimum = GetSeatMinimum(providerPlans, PlanType.EnterpriseMonthly);
        Gateway = provider.Gateway;
        GatewayCustomerId = provider.GatewayCustomerId;
        GatewaySubscriptionId = provider.GatewaySubscriptionId;
        GatewayCustomerUrl = gatewayCustomerUrl;
        GatewaySubscriptionUrl = gatewaySubscriptionUrl;
        Type = provider.Type;

        if (Type == ProviderType.MultiOrganizationEnterprise)
        {
            var plan = providerPlans.SingleOrDefault();
            EnterpriseMinimumSeats = plan?.SeatMinimum ?? 0;
            Plan = plan?.PlanType;
        }
    }

    [Display(Name = "Billing Email")]
    public string BillingEmail { get; set; }
    [Display(Name = "Billing Phone Number")]
    public string BillingPhone { get; set; }
    [Display(Name = "Business Name")]
    public string BusinessName { get; set; }
    public string Name { get; set; }
    [Display(Name = "Teams (Monthly) Seat Minimum")]
    public int TeamsMonthlySeatMinimum { get; set; }

    [Display(Name = "Enterprise (Monthly) Seat Minimum")]
    public int EnterpriseMonthlySeatMinimum { get; set; }
    [Display(Name = "Gateway")]
    public GatewayType? Gateway { get; set; }
    [Display(Name = "Gateway Customer Id")]
    public string GatewayCustomerId { get; set; }
    [Display(Name = "Gateway Subscription Id")]
    public string GatewaySubscriptionId { get; set; }
    public string GatewayCustomerUrl { get; }
    public string GatewaySubscriptionUrl { get; }
    [Display(Name = "Provider Type")]
    public ProviderType Type { get; set; }

    [Display(Name = "Plan")]
    public PlanType? Plan { get; set; }

    [Display(Name = "Enterprise Seats Minimum")]
    public int? EnterpriseMinimumSeats { get; set; }

    public virtual Provider ToProvider(Provider existingProvider)
    {
        existingProvider.BillingEmail = BillingEmail?.ToLowerInvariant().Trim();
        existingProvider.BillingPhone = BillingPhone?.ToLowerInvariant().Trim();
        switch (Type)
        {
            case ProviderType.Msp:
                existingProvider.Gateway = Gateway;
                existingProvider.GatewayCustomerId = GatewayCustomerId;
                existingProvider.GatewaySubscriptionId = GatewaySubscriptionId;
                break;
        }
        return existingProvider;
    }

    private static int GetSeatMinimum(IEnumerable<ProviderPlan> providerPlans, PlanType planType)
        => providerPlans.FirstOrDefault(providerPlan => providerPlan.PlanType == planType)?.SeatMinimum ?? 0;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        switch (Type)
        {
            case ProviderType.Reseller:
                if (string.IsNullOrWhiteSpace(BillingEmail))
                {
                    var billingEmailDisplayName = nameof(BillingEmail).GetDisplayAttribute<CreateProviderModel>()?.GetName() ?? nameof(BillingEmail);
                    yield return new ValidationResult($"The {billingEmailDisplayName} field is required.");
                }
                break;
            case ProviderType.MultiOrganizationEnterprise:
                if (Plan == null)
                {
                    var displayName = nameof(Plan).GetDisplayAttribute<CreateProviderModel>()?.GetName() ?? nameof(Plan);
                    yield return new ValidationResult($"The {displayName} field is required.");
                }
                if (EnterpriseMinimumSeats == null)
                {
                    var displayName = nameof(EnterpriseMinimumSeats).GetDisplayAttribute<CreateProviderModel>()?.GetName() ?? nameof(EnterpriseMinimumSeats);
                    yield return new ValidationResult($"The {displayName} field is required.");
                }
                if (EnterpriseMinimumSeats < 0)
                {
                    var displayName = nameof(EnterpriseMinimumSeats).GetDisplayAttribute<CreateProviderModel>()?.GetName() ?? nameof(EnterpriseMinimumSeats);
                    yield return new ValidationResult($"The {displayName} field cannot be less than 0.");
                }
                break;
        }
    }
}
