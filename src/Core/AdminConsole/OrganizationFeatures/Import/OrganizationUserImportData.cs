#nullable enable

using Bit.Core.Models.Business;
namespace Bit.Core.Models.Data.Organizations.OrganizationUsers;

public class OrganizationUserImportData
{
    /// <summary>
    /// Set of user ExternalIds that are being imported
    /// </summary>
    public readonly HashSet<string> ImportedExternalIds;
    /// <summary>
    /// All existing OrganizationUsers for the organization
    /// </summary>
    public readonly ICollection<OrganizationUserUserDetails> ExistingUsers;
    /// <summary>
    /// Existing OrganizationUsers with ExternalIds set.
    /// </summary>
    public readonly IEnumerable<OrganizationUserUserDetails> ExistingExternalUsers;
    /// <summary>
    /// Mapping of an existing users's ExternalId to their Id
    /// </summary>
    public readonly Dictionary<string, Guid> ExistingExternalUsersIdDict;

    public OrganizationUserImportData(ICollection<OrganizationUserUserDetails> existingUsers, IEnumerable<ImportedOrganizationUser> importedUsers)
    {
        ImportedExternalIds = new HashSet<string>(importedUsers?.Select(u => u.ExternalId) ?? new List<string>());
        ExistingUsers = existingUsers;
        ExistingExternalUsers = ExistingUsers.Where(u => !string.IsNullOrWhiteSpace(u.ExternalId)).ToList();
        ExistingExternalUsersIdDict = ExistingExternalUsers.ToDictionary(u => u.ExternalId, u => u.Id);
    }
}
