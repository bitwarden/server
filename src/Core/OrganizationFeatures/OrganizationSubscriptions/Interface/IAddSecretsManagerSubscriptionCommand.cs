using Bit.Core.Entities;
using Bit.Core.Models.Data.Organizations.OrganizationUsers;

namespace Bit.Core.OrganizationFeatures.OrganizationSubscriptions.Interface;

/// <summary>
/// This is only for adding SM to an existing organization
/// </summary>
public interface IAddSecretsManagerSubscriptionCommand
{
    Task<OrganizationUserOrganizationDetails> SignUpAsync(Organization organization, int additionalSmSeats, int additionalServiceAccounts, Guid userId);
}
