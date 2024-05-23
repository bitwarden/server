#nullable enable
using Bit.Core.SecretsManager.Models.Data;

namespace Bit.Core.SecretsManager.Commands.AccessPolicies.Interfaces;

public interface IUpdateServiceAccountGrantedPoliciesCommand
{
    Task UpdateAsync(ServiceAccountGrantedPoliciesUpdates grantedPoliciesUpdates);
}
