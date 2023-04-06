using Bit.Core.SecretsManager.Enums;

namespace Bit.Core.SecretsManager.Models.Data;

public class AccessCheck
{
    public AccessOperationType AccessOperationType { get; set; }
    public Guid OrganizationId { get; set; }
    public Guid TargetId { get; set; }
    public Guid UserId { get; set; }
}

public class SecretAccessCheck
{
    public AccessOperationType AccessOperationType { get; set; }
    public Guid OrganizationId { get; set; }
    public Guid? CurrentSecretId { get; set; }
    public Guid? TargetProjectId { get; set; }
    public Guid UserId { get; set; }
}
