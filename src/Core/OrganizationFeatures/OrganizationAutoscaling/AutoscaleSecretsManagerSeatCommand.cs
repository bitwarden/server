using Bit.Core.Entities;
using Bit.Core.Exceptions;
using Bit.Core.OrganizationFeatures.OrganizationAutoscaling.Interfaces;
using Bit.Core.Utilities;

namespace Bit.Core.OrganizationFeatures.OrganizationAutoscaling;

public class AutoscaleSecretsManagerSeatCommand : IAutoscaleSecretsManagerSeatCommand
{
    public Organization AutoscaleSeatsAsync(Organization organization, int? maxAutoscaleSeats)
    {
        if (organization == null)
        {
            throw new NotFoundException();
        }
        
        if (maxAutoscaleSeats.HasValue && organization.SmSeats.HasValue &&
            maxAutoscaleSeats.Value < organization.SmSeats.Value)
        {
            throw new BadRequestException($"Cannot set max Secrets Manager seat autoscaling below current Secrets Manager seat count.");
        }

        var plan = StaticStore.SecretManagerPlans.FirstOrDefault(p => p.Type == organization.PlanType);

        if (plan == null)
        {
            throw new BadRequestException("Existing plan not found.");
        }

        if (!plan.AllowSeatAutoscale)
        {
            throw new BadRequestException("Your plan does not allow Secrets Manager seat autoscaling.");
        }

        if (plan.MaxUsers.HasValue && maxAutoscaleSeats.HasValue &&
            maxAutoscaleSeats > plan.MaxUsers)
        {
            throw new BadRequestException(string.Concat(
                $"Your plan has a Secrets Manager seat limit of {plan.MaxUsers}, ",
                $"but you have specified a max autoscale count of {maxAutoscaleSeats}.",
                "Reduce your max autoscale seat count."));
        }

        organization.MaxAutoscaleSmSeats = maxAutoscaleSeats;

        return organization;
    }
}
