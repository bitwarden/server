using Bit.Core.Entities;

namespace Bit.Core.OrganizationFeatures.OrganizationSmSubscription.Interface;

public interface ISubscribeOrganziationSmCommand
{
    Task<Tuple<bool, string>> SignUpAsync(Guid organizationId, int additionalSeats,
        int additionalServiceAccounts);
}
