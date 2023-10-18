namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Interfaces;

public interface ICountNewSmSeatsRequiredQuery
{
    public Task<int> CountNewSmSeatsRequiredAsync(Guid organizationId, int usersToAdd);
}
