using Bit.Core.AdminConsole.Entities.Provider;
using Bit.Core.AdminConsole.Enums.Provider;
using Bit.Core.AdminConsole.Models.Data.Provider;
using Bit.Core.Billing.Entities;
using Bit.Core.Billing.Enums;
using Microsoft.OpenApi.Extensions;

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
        ProviderAdmins = providerUsers.Where(u => u.Type == ProviderUserType.ProviderAdmin);
        ProviderOrganizations = organizations.Where(o => o.ProviderId == provider.Id);

        var usedEnterpriseSeats = ProviderOrganizations.Where(po => po.Plan.Contains("Enterprise")).Sum(po => po.OccupiedSeats);
        var usedTeamsSeats = ProviderOrganizations.Where(po => po.Plan.Contains("Teams")).Sum(po => po.OccupiedSeats);

        const string teamsSubscription = "Teams Subscription";
        const string enterpriseSubscription = "Enterprise Subscription";

        if (Provider.Type == ProviderType.Msp)
        {
            var teamsProviderPlan = providerPlans.First(plan => plan.PlanType == PlanType.TeamsMonthly);
            if (teamsProviderPlan.IsConfigured())
            {
                ProviderPlanViewModels.Add(new ProviderPlanViewModel(teamsSubscription, teamsProviderPlan, usedTeamsSeats!.Value));
            }

            var enterpriseProviderPlan = providerPlans.First(plan => plan.PlanType == PlanType.EnterpriseMonthly);
            if (enterpriseProviderPlan.IsConfigured())
            {
                ProviderPlanViewModels.Add(new ProviderPlanViewModel(enterpriseSubscription, enterpriseProviderPlan, usedEnterpriseSeats!.Value));
            }
        }
        else if (Provider.Type == ProviderType.MultiOrganizationEnterprise)
        {
            var enterpriseProviderPlan = providerPlans.First();
            if (enterpriseProviderPlan.IsConfigured())
            {
                ProviderPlanViewModels.Add(new ProviderPlanViewModel(enterpriseSubscription, enterpriseProviderPlan, usedEnterpriseSeats!.Value));
            }
        }
    }

    public int UserCount { get; set; }
    public Provider Provider { get; set; }
    public IEnumerable<ProviderUserUserDetails> ProviderAdmins { get; set; }
    public IEnumerable<ProviderOrganizationOrganizationDetails> ProviderOrganizations { get; set; }
    public List<ProviderPlanViewModel> ProviderPlanViewModels { get; set; } = [];

    public class ProviderPlanViewModel
    {
        public string Name { get; set; }
        public int PurchasedSeats { get; set; }
        public int AssignedSeats { get; set; }
        public int UsedSeats { get; set; }
        public int RemainingSeats { get; set; }

        public ProviderPlanViewModel(
            string name,
            ProviderPlan providerPlan,
            int usedSeats)
        {
            var purchasedSeats = providerPlan.SeatMinimum!.Value + providerPlan.PurchasedSeats!.Value;

            Name = name;
            PurchasedSeats = purchasedSeats;
            AssignedSeats = providerPlan.AllocatedSeats!.Value;
            UsedSeats = usedSeats;
            RemainingSeats = purchasedSeats - AssignedSeats;
        }
    }
}
