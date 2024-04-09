using System.ComponentModel.DataAnnotations;
using Bit.Core.AdminConsole.Entities.Provider;
using Bit.Core.AdminConsole.Models.Data.Provider;
using Bit.Core.Billing.Entities;
using Bit.Core.Enums;

namespace Bit.Admin.AdminConsole.Models;

public class ProviderEditModel : ProviderViewModel
{
    public ProviderEditModel() { }

    public ProviderEditModel(Provider provider, IEnumerable<ProviderUserUserDetails> providerUsers,
        IEnumerable<ProviderOrganizationOrganizationDetails> organizations, IEnumerable<ProviderPlan> providerPlans)
        : base(provider, providerUsers, organizations)
    {
        Name = provider.DisplayName();
        BusinessName = provider.DisplayBusinessName();
        BillingEmail = provider.BillingEmail;
        BillingPhone = provider.BillingPhone;
        TeamsMinimumSeats = GetMinimumSeats(providerPlans, PlanType.TeamsMonthly);
        EnterpriseMinimumSeats = GetMinimumSeats(providerPlans, PlanType.EnterpriseMonthly);
    }

    [Display(Name = "Billing Email")]
    public string BillingEmail { get; set; }
    [Display(Name = "Billing Phone Number")]
    public string BillingPhone { get; set; }
    [Display(Name = "Business Name")]
    public string BusinessName { get; set; }
    public string Name { get; set; }
    [Display(Name = "Teams minimum seats")]
    public int TeamsMinimumSeats { get; set; }

    [Display(Name = "Enterprise minimum seats")]
    public int EnterpriseMinimumSeats { get; set; }
    [Display(Name = "Events")]

    public IEnumerable<ProviderPlan> ToProviderPlan(IEnumerable<ProviderPlan> existingProviderPlans)
    {
        var providerPlans = existingProviderPlans.ToList();
        foreach (var existingProviderPlan in providerPlans)
        {
            existingProviderPlan.SeatMinimum = existingProviderPlan.PlanType switch
            {
                PlanType.TeamsMonthly => TeamsMinimumSeats,
                PlanType.EnterpriseMonthly => EnterpriseMinimumSeats,
                _ => existingProviderPlan.SeatMinimum
            };
        }
        return providerPlans;
    }

    public Provider ToProvider(Provider existingProvider)
    {
        existingProvider.BillingEmail = BillingEmail?.ToLowerInvariant()?.Trim();
        existingProvider.BillingPhone = BillingPhone?.ToLowerInvariant()?.Trim();
        return existingProvider;
    }


    private int GetMinimumSeats(IEnumerable<ProviderPlan> providerPlans, PlanType planType)
    {
        return (from providerPlan in providerPlans where providerPlan.PlanType == planType select (int)providerPlan.SeatMinimum).FirstOrDefault();
    }
}
