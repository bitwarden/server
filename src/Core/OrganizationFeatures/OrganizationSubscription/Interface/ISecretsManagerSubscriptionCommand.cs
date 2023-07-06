using Bit.Core.Entities;

namespace Bit.Core.OrganizationFeatures.OrganizationSubscription.Interface;

public interface ISecretsManagerSubscriptionCommand
{
    Task<Tuple<Organization, OrganizationUser>> SignUpAsync(Guid organizationId, int additionalSeats,
        int additionalServiceAccounts);
}
