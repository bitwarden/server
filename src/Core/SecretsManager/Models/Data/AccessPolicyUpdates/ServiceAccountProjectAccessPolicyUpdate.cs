#nullable enable
using Bit.Core.SecretsManager.Entities;
using Bit.Core.SecretsManager.Enums.AccessPolicies;

namespace Bit.Core.SecretsManager.Models.Data.AccessPolicyUpdates;

public class ServiceAccountProjectAccessPolicyUpdate
{
    public AccessPolicyOperation Operation { get; set; }
    public required ServiceAccountProjectAccessPolicy AccessPolicy { get; set; }
}
