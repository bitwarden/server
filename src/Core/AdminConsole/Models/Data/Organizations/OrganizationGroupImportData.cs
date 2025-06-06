using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Models.Business;

namespace Bit.Core.Models.Data.Organizations;

public class OrganizationGroupImportData
{
    public readonly IEnumerable<ImportedGroup> Groups;
    public readonly ICollection<Group> ExistingGroups;
    public readonly IEnumerable<Group> ExistingExternalGroups;
    public readonly IDictionary<string, ImportedGroup> GroupsDict;

    public OrganizationGroupImportData(IEnumerable<ImportedGroup> groups, ICollection<Group> existingGroups)
    {
        Groups = groups;
        GroupsDict = groups.ToDictionary(g => g.Group.ExternalId);
        ExistingGroups = existingGroups;
        ExistingExternalGroups = existingGroups.Where(u => !string.IsNullOrWhiteSpace(u.ExternalId)).ToList();
    }
}
