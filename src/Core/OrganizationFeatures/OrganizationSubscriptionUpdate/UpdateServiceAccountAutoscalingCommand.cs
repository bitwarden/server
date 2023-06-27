using Bit.Core.Entities;
using Bit.Core.Exceptions;
using Bit.Core.OrganizationFeatures.OrganizationSubscriptionUpdate.Interface;
using Bit.Core.Services;
using Bit.Core.Utilities;

namespace Bit.Core.OrganizationFeatures.OrganizationSubscriptionUpdate;

public class UpdateServiceAccountAutoscalingCommand : IUpdateServiceAccountAutoscalingCommand
{
    private readonly IOrganizationService _organizationService;

    public UpdateServiceAccountAutoscalingCommand(IOrganizationService organizationService)
    {
        _organizationService = organizationService;
    }
    
    public async Task UpdateServiceAccountAutoscalingAsync(Organization organization, int? maxAutoscaleServiceAccounts)
    {
        if (maxAutoscaleServiceAccounts.HasValue &&
            organization.SmServiceAccounts.HasValue &&
            maxAutoscaleServiceAccounts.Value < organization.SmServiceAccounts.Value)
        {
            throw new BadRequestException(
                $"Cannot set max service account autoscaling below current service account count.");
        }


        var plan = StaticStore.SecretManagerPlans.FirstOrDefault(p => p.Type == organization.PlanType);

        if (plan == null)
        {
            throw new BadRequestException("Existing plan not found.");
        }

        if (!plan.AllowSeatAutoscale)
        {
            throw new BadRequestException("Your plan does not allow seat autoscaling.");
        }

        if (plan.MaxServiceAccounts.HasValue && maxAutoscaleServiceAccounts.HasValue &&
            maxAutoscaleServiceAccounts > plan.MaxServiceAccounts)
        {
            throw new BadRequestException(string.Concat(
                $"Your plan has a service account limit of {plan.MaxUsers}, ",
                $"but you have specified a max autoscale count of {maxAutoscaleServiceAccounts}.",
                "Reduce your max autoscale seat count."));
        }
        organization.MaxAutoscaleSmServiceAccounts = maxAutoscaleServiceAccounts;

        await _organizationService.ReplaceAndUpdateCacheAsync(organization);
    }
}
