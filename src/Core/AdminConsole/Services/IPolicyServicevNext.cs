using Bit.Core.AdminConsole.Entities;
using Bit.Core.Services;

namespace Bit.Core.AdminConsole.Services;

public interface IPolicyServicevNext
{
    Task SaveAsync(Policy policy, IUserService userService, IOrganizationService organizationService,
        Guid? savingUserId);
}
