namespace Bit.Api.Pam.Models.Request;

/// <summary>
/// A request to revoke an active lease early. <see cref="Reason"/> is optional and retained for the audit trail.
/// </summary>
public class LeaseRevokeRequestModel
{
    public string? Reason { get; set; }
}
