using Bit.Core.Entities;

namespace Bit.Core.OrganizationFeatures.OrganizationSubscriptions.Interface;

public interface IAddSecretsManagerSubscriptionCommand
{
    Task<Tuple<Organization, OrganizationUser>> SignUpAsync(Guid organizationId, int additionalSeats,
        int additionalServiceAccounts);
}
