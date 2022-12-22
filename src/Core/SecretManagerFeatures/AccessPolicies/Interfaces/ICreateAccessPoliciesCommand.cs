#nullable enable
using Bit.Core.Entities;

namespace Bit.Core.SecretManagerFeatures.AccessPolicies.Interfaces;

public interface ICreateAccessPoliciesCommand
{
    Task<List<BaseAccessPolicy>> CreateAsync(List<BaseAccessPolicy> accessPolicies);
}
