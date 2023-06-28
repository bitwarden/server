using Bit.Core.Entities;

namespace Bit.Core.OrganizationFeatures.OrganizationSmSubscription.Interface;

public interface ISubscribeOrganziationSecretsManagerCommand
{
    Task<Tuple<Organization, OrganizationUser>> SignUpAsync(string organizationId, int additionalSeats,
        int additionalServiceAccounts, bool provider = false);
}
