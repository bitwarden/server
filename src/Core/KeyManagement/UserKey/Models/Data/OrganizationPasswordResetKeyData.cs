namespace Bit.Core.KeyManagement.UserKey.Models.Data;

public class OrganizationPasswordResetKeyData
{
    public Guid OrganizationId { get; set; }
    public required string OrganizationName { get; set; }
    public required string OrganizationPublicKey { get; set; }
}
