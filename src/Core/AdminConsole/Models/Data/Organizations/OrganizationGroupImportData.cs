using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Models.Business;

namespace Bit.Core.Models.Data.Organizations;

public class OrganizationGroupImportData
{
    public IEnumerable<ImportedGroup> Groups { get; set; }
    public ICollection<Group> ExistingGroups { get; set; }
    public IEnumerable<Group> ExistingExternalGroups { get; set; }
    public IDictionary<string, ImportedGroup> GroupsDict { get; set; }
}
