namespace Bit.Core.SecretsManager.Models.Data;

public enum OperationType
{
    CreateAccessToken,
    RevokeAccessToken,
    CreateServiceAccount,
    UpdateServiceAccount,
    CreateProject,
    UpdateProject,
    CreateSecret,
    UpdateSecret,
}

public class AccessCheck
{
    public OperationType OperationType { get; set; }
    public Guid OrganizationId { get; set; }
    public Guid TargetId { get; set; }
    public Guid UserId { get; set; }
}

public class SecretAccessCheck
{
    public OperationType OperationType { get; set; }
    public Guid OrganizationId { get; set; }
    public Guid? CurrentSecretId { get; set; }
    public Guid? TargetProjectId { get; set; }
    public Guid UserId { get; set; }
}
