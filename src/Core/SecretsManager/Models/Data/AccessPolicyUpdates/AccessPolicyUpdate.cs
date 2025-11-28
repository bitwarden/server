#nullable enable
using Bit.Core.SecretsManager.Entities;
using Bit.Core.SecretsManager.Enums.AccessPolicies;

namespace Bit.Core.SecretsManager.Models.Data.AccessPolicyUpdates;

public class UserSecretAccessPolicyUpdate
{
    public AccessPolicyOperation Operation { get; set; }
    public required UserSecretAccessPolicy AccessPolicy { get; set; }
}

public class GroupSecretAccessPolicyUpdate
{
    public AccessPolicyOperation Operation { get; set; }
    public required GroupSecretAccessPolicy AccessPolicy { get; set; }
}

public class ServiceAccountSecretAccessPolicyUpdate
{
    public AccessPolicyOperation Operation { get; set; }
    public required ServiceAccountSecretAccessPolicy AccessPolicy { get; set; }
}
