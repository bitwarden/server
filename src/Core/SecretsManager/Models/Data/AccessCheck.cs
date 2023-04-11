namespace Bit.Core.SecretsManager.Models.Data;

public class AccessCheck
{
    public Guid OrganizationId { get; set; }
    public Guid TargetId { get; set; }
    public Guid UserId { get; set; }
}
