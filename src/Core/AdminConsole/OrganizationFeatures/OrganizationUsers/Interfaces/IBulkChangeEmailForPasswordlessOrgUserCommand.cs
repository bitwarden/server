namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Interfaces;

public interface IBulkChangeEmailForPasswordlessOrgUserCommand
{
    Task<IEnumerable<(Guid OrganizationUserId, string ErrorMessage)>> BulkChangeOrganizationUserEmailAsync(
        Guid organizationId,
        IEnumerable<(Guid OrganizationUserId, string NewEmail)> requests);
}
