using Bit.Core.AdminConsole.OrganizationAuth.Models;

namespace Bit.Core.AdminConsole.OrganizationAuth.Interfaces;

public interface IUpdateOrganizationAuthRequestCommand
{
    Task UpdateAsync(Guid requestId, Guid userId, bool requestApproved, string encryptedUserKey);
    Task UpdateAsync(Guid organizationId, IEnumerable<OrganizationAuthRequestUpdate> authRequestUpdates);
}
