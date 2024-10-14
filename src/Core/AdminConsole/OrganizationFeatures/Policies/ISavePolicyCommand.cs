using Bit.Core.AdminConsole.OrganizationFeatures.Policies.Models;
using Bit.Core.Services;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Policies;

public interface ISavePolicyCommand
{
    Task SaveAsync(PolicyUpdate policy, IOrganizationService organizationService, Guid? savingUserId);
}
