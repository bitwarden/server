using Bit.Core.AdminConsole.Entities;
using Bit.Core.Models.Data.Organizations;

#nullable enable

namespace Bit.Core.Repositories;

public interface IOrganizationRepository : IRepository<Organization, Guid>
{
    Task<Organization?> GetByIdentifierAsync(string identifier);
    Task<ICollection<Organization>> GetManyByEnabledAsync();
    Task<ICollection<Organization>> GetManyByUserIdAsync(Guid userId);
    Task<ICollection<Organization>> SearchAsync(string name, string userEmail, bool? paid, int skip, int take);
    Task UpdateStorageAsync(Guid id);
    Task<ICollection<OrganizationAbility>> GetManyAbilitiesAsync();
    Task<Organization?> GetByLicenseKeyAsync(string licenseKey);
    Task<SelfHostedOrganizationDetails?> GetSelfHostedOrganizationDetailsById(Guid id);
    Task<ICollection<Organization>> SearchUnassignedToProviderAsync(string name, string ownerEmail, int skip, int take);
    Task<IEnumerable<string>> GetOwnerEmailAddressesById(Guid organizationId);

    /// <summary>
    /// Retrieves organizations where the user's email domain matches a verified organization domain.
    /// </summary>
    /// <remarks>
    /// Only returns organizations where:
    /// 1. The domain in the user's email matches a verified organization domain
    /// 2. The user is an existing organization member with either 'Confirmed' or 'Revoked' status
    /// </remarks>
    Task<ICollection<Organization>> GetByVerifiedUserEmailDomainAsync(Guid userId);
}
