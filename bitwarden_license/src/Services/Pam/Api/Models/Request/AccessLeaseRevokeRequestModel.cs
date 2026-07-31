namespace Bit.Services.Pam.Api.Models.Request;

/// <summary>
/// A request to revoke an active lease early. <see cref="Reason"/> is optional and retained for the audit trail.
/// </summary>
public class AccessLeaseRevokeRequestModel
{
    /// <summary>
    /// An optional note explaining the revocation. Recorded on the audit trail; not surfaced on the lease itself.
    /// </summary>
    public string? Reason { get; set; }
}
