using Bit.Core.Entities;

namespace Bit.Core.OrganizationFeatures.OrganizationSubscriptions.Interface;

public interface IAddSecretsManagerSubscriptionCommand
{
    Task<Organization> SignUpAsync(Organization organization, int additionalSeats, int additionalServiceAccounts);
}
