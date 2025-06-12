#nullable enable

using Bit.Core.AdminConsole.Models.Business;
using Bit.Core.Models.Business;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Interfaces;

public interface IImportOrganizationUsersAndGroupsCommand
{
    Task ImportAsync(Guid organizationId,
        IEnumerable<ImportedGroup> groups,
        IEnumerable<ImportedOrganizationUser> newUsers,
        IEnumerable<string> removeUserExternalIds,
        bool overwriteExisting
    );
}
