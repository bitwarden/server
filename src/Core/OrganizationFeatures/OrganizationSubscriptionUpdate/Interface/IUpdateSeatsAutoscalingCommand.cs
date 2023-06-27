using Bit.Core.Entities;

namespace Bit.Core.OrganizationFeatures.OrganizationSubscriptionUpdate.Interface;

public interface IUpdateSeatsAutoscalingCommand
{
    Task UpdateSeatsAutoscalingAsync(Organization organization, int? maxAutoscaleSeats);
}
