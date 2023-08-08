namespace Bit.Core.AdminConsole.OrganizationAuth.Interfaces;

public interface IUpdateOrganizationAuthRequestCommand
{
    Task UpdateAsync(Guid requestId, Guid userId, bool requestApproved, string encryptedUserKey);
}
