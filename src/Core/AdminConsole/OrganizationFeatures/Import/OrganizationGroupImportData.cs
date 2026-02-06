#nullable enable

using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Models.Business;

namespace Bit.Core.Models.Data.Organizations;

/// <summary>
/// Represents the data required to import organization groups,
/// including newly imported groups and existing groups within the organization.
/// </summary>
public class OrganizationGroupImportData
{
    /// <summary>
    /// The collection of groups that are being imported.
    /// </summary>
    public readonly IEnumerable<ImportedGroup> Groups;

    /// <summary>
    /// Collection of groups that already exist in the organization.
    /// </summary>
    public readonly ICollection<Group> ExistingGroups;

    /// <summary>
    /// Existing groups with ExternalId set.
    /// </summary>
    public readonly IEnumerable<Group> ExistingExternalGroups;

    /// <summary>
    /// Mapping of imported groups keyed by their ExternalId.
    /// </summary>
    public readonly IDictionary<string, ImportedGroup> GroupsDict;

    public OrganizationGroupImportData(IEnumerable<ImportedGroup> groups, ICollection<Group> existingGroups)
    {
        Groups = groups;
        GroupsDict = groups.ToDictionary(g => g.Group.ExternalId!);
        ExistingGroups = existingGroups;
        ExistingExternalGroups = existingGroups.Where(u => !string.IsNullOrWhiteSpace(u.ExternalId)).ToList();
    }
}
