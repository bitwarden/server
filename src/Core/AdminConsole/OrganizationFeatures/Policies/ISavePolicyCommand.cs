using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.Models;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Policies;

public interface ISavePolicyCommand
{
    Task<Policy> SaveAsync(PolicyUpdate policy);
}
