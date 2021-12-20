using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Bit.Core.Models.Business;

namespace Bit.Core.OrganizationFeatures.OrgUser
{
    public interface IOrganizationUserImportCommand
    {
        Task ImportAsync(Guid organizationId, Guid? importingUserId, IEnumerable<ImportedGroup> groups,
            IEnumerable<ImportedOrganizationUser> newUsers, IEnumerable<string> removeUserExternalIds,
            bool overwriteExisting);
    }
}
