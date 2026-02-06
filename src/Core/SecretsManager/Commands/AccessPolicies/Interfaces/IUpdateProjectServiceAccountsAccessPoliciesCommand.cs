#nullable enable
using Bit.Core.SecretsManager.Models.Data.AccessPolicyUpdates;

namespace Bit.Core.SecretsManager.Commands.AccessPolicies.Interfaces;

public interface IUpdateProjectServiceAccountsAccessPoliciesCommand
{
    Task UpdateAsync(ProjectServiceAccountsAccessPoliciesUpdates accessPoliciesUpdates);
}
