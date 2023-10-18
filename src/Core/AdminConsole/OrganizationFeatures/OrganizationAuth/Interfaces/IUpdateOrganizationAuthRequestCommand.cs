namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationAuth.Interfaces;

public interface IUpdateOrganizationAuthRequestCommand
{
    Task UpdateAsync(Guid requestId, Guid userId, bool requestApproved, string encryptedUserKey);
}
