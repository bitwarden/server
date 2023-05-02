using Bit.Core.Models.Business;

namespace Bit.Core.OrganizationFeatures.Import.Interfaces;

public interface IImportOrganizationCommand
{
    Task ImportAsync(Guid organizationId, Guid? importingUserId, IEnumerable<ImportedGroup> groups,
        IEnumerable<ImportedOrganizationUser> newUsers, IEnumerable<string> removeUserExternalIds,
        bool overwriteExisting);
}
