using Bit.Core.Entities;

namespace Bit.Core.OrganizationFeatures.OrganizationSubscriptions.Interface;

public interface IAddSecretsManagerSubscriptionCommand
{
    Task<Tuple<Organization, OrganizationUser>> SignUpAsync(Organization organization, int additionalSeats,
        int additionalServiceAccounts);
}
