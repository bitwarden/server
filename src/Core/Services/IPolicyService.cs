using Bit.Core.Entities;

namespace Bit.Core.Services;

public interface IPolicyService
{
    Task SaveAsync(Policy policy, IUserService userService, IOrganizationService organizationService,
        Guid? savingUserId);
}
