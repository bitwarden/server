using Bit.Core.AdminConsole.Entities;

namespace Bit.Core.AdminConsole.Services;

public interface IOrganizationCache
{
    Task<Organization?> GetAsync(Guid organizationId);
}
