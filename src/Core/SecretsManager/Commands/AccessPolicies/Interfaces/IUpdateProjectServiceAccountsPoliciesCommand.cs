#nullable enable
using Bit.Core.SecretsManager.Models.Data;

namespace Bit.Core.SecretsManager.Commands.AccessPolicies.Interfaces;

public interface IUpdateProjectServiceAccountsPoliciesCommand
{
    Task UpdateAsync(ProjectServiceAccountsPoliciesUpdates policiesUpdates);
}
