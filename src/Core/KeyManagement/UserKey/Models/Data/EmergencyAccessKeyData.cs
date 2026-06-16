namespace Bit.Core.KeyManagement.UserKey.Models.Data;

public class EmergencyAccessKeyData
{
    public Guid Id { get; set; }
    public Guid? GranteeId { get; set; }
    public string? GranteeName { get; set; }
    public string? GranteeEmail { get; set; }
    public required string PublicKey { get; set; }
}
