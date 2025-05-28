using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums.Provider;
using Bit.Core.Models.Data.Organizations;
using Bit.Core.Models.Data.Organizations.OrganizationUsers;

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
    /// Gets the organizations that have a verified domain matching the user's email domain.
    /// </summary>
    Task<ICollection<Organization>> GetByVerifiedUserEmailDomainAsync(Guid userId);
    Task<ICollection<Organization>> GetAddableToProviderByUserIdAsync(Guid userId, ProviderType providerType);
    Task<ICollection<Organization>> GetManyByIdsAsync(IEnumerable<Guid> ids);

    /// <summary>
    /// Returns the number of occupied seats for an organization.
    /// OrganizationUsers occupy a seat, unless they are revoked.
    /// As of https://bitwarden.atlassian.net/browse/PM-17772, a seat is also occupied by a Families for Enterprise sponsorship sent by an
    /// organization admin, even if the user sent the invitation doesn't have a corresponding OrganizationUser in the Enterprise organization.
    /// </summary>
    /// <param name="organizationId">The ID of the organization to get the occupied seat count for.</param>
    /// <returns>The number of occupied seats for the organization.</returns>
    Task<OrganizationSeatCounts> GetOccupiedSeatCountByOrganizationIdAsync(Guid organizationId);
}
