using Bit.Core.Entities;

namespace Bit.Core.OrganizationFeatures.OrganizationSubscriptionUpdate.Interface;

public interface IUpdateServiceAccountAutoscalingCommand
{
    Task UpdateServiceAccountAutoscalingAsync(Organization organization, int? maxAutoscaleServiceAccounts);
}
