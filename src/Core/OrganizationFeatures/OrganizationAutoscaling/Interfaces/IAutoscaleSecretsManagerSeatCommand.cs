using Bit.Core.Entities;

namespace Bit.Core.OrganizationFeatures.OrganizationAutoscaling.Interfaces;

public interface IAutoscaleSecretsManagerSeatCommand
{
    Organization AutoscaleSeatsAsync(Organization organization, int? maxAutoscaleSeats);
}
