using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Models.Business;

namespace Bit.Core.Models.Data.Organizations;

public class OrganizationGroupImportData
{
    public IEnumerable<ImportedGroup> Groups { get; init; }
    public ICollection<Group> ExistingGroups { get; init; }
    public IEnumerable<Group> ExistingExternalGroups { get; init; }
    public IDictionary<string, ImportedGroup> GroupsDict { get; init; }

    public OrganizationGroupImportData(IEnumerable<ImportedGroup> groups, ICollection<Group> existingGroups)
    {
        Groups = groups;
        GroupsDict = groups.ToDictionary(g => g.Group.ExternalId);
        ExistingGroups = existingGroups;
        ExistingExternalGroups = GetExistingExternalGroups(existingGroups);
    }

    private IEnumerable<Group> GetExistingExternalGroups(ICollection<Group> existingGroups)
    {
        return existingGroups
            .Where(u => !string.IsNullOrWhiteSpace(u.ExternalId))
            .ToList();
    }
}
