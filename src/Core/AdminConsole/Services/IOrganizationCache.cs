using Bit.Core.AdminConsole.Entities;

namespace Bit.Core.Services;

public interface IOrganizationCache
{
    Task<Organization?> GetAsync(Guid organizationId);
}
