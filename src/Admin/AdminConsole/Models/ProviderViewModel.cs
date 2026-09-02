// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using Bit.Admin.Billing.Models;
using Bit.Core.AdminConsole.Entities.Provider;
using Bit.Core.AdminConsole.Enums.Provider;
using Bit.Core.AdminConsole.Models.Data.Provider;
using Bit.Core.Billing.Enums;
using Bit.Core.Billing.Providers.Entities;

namespace Bit.Admin.AdminConsole.Models;

public class ProviderViewModel
{
    public ProviderViewModel() { }

    public ProviderViewModel(
        Provider provider,
        IEnumerable<ProviderUserUserDetails> providerUsers,
        IEnumerable<ProviderOrganizationOrganizationDetails> organizations,
        IReadOnlyCollection<ProviderPlan> providerPlans)
    {
        Provider = provider;
        UserCount = providerUsers.Count();
        ProviderUsers = providerUsers;
        ProviderOrganizations = organizations.Where(o => o.ProviderId == provider.Id);

        if (Provider.Type == ProviderType.Msp)
        {
            var usedTeamsSeats = ProviderOrganizations.Where(po => po.PlanType == PlanType.TeamsMonthly)
                .Sum(po => po.OccupiedSeats) ?? 0;
            var teamsProviderPlan = providerPlans.FirstOrDefault(plan => plan.PlanType == PlanType.TeamsMonthly);
            if (teamsProviderPlan != null && teamsProviderPlan.IsConfigured())
            {
                ProviderPlanViewModels.Add(new ProviderPlanViewModel("Teams (Monthly) Subscription", teamsProviderPlan, usedTeamsSeats));
            }

            var usedEnterpriseSeats = ProviderOrganizations.Where(po => po.PlanType == PlanType.EnterpriseMonthly)
                .Sum(po => po.OccupiedSeats) ?? 0;
            var enterpriseProviderPlan = providerPlans.FirstOrDefault(plan => plan.PlanType == PlanType.EnterpriseMonthly);
            if (enterpriseProviderPlan != null && enterpriseProviderPlan.IsConfigured())
            {
                ProviderPlanViewModels.Add(new ProviderPlanViewModel("Enterprise (Monthly) Subscription", enterpriseProviderPlan, usedEnterpriseSeats));
            }
        }
        else if (Provider.Type == ProviderType.BusinessUnit)
        {
            var usedEnterpriseSeats = ProviderOrganizations.Where(po => po.PlanType == PlanType.EnterpriseMonthly)
                .Sum(po => po.OccupiedSeats).GetValueOrDefault(0);
            var enterpriseProviderPlan = providerPlans.FirstOrDefault();
            if (enterpriseProviderPlan != null && enterpriseProviderPlan.IsConfigured())
            {
                var planLabel = enterpriseProviderPlan.PlanType switch
                {
                    PlanType.EnterpriseMonthly => "Enterprise (Monthly) Subscription",
                    PlanType.EnterpriseAnnually => "Enterprise (Annually) Subscription",
                    _ => string.Empty
                };

                ProviderPlanViewModels.Add(new ProviderPlanViewModel(planLabel, enterpriseProviderPlan, usedEnterpriseSeats));
            }
        }
    }

    public int UserCount { get; set; }
    public Provider Provider { get; set; }
    public IEnumerable<ProviderUserUserDetails> ProviderUsers { get; set; }
    public IEnumerable<ProviderOrganizationOrganizationDetails> ProviderOrganizations { get; set; }
    public List<ProviderPlanViewModel> ProviderPlanViewModels { get; set; } = [];
}
