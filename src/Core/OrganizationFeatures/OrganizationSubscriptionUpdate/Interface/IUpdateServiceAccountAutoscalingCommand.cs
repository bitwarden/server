using Bit.Core.Entities;

namespace Bit.Core.OrganizationFeatures.OrganizationSubscriptionUpdate.Interface;

public interface IUpdateServiceAccountAutoscaling
{
    Task UpdateServiceAccountAutoscalingAsync(Organization organization, int? maxAutoscaleServiceAccounts);
}
