namespace Bit.Services.Pam.Api.Models.Request;

/// <summary>
/// A request to revoke an active lease early.
/// </summary>
public class AccessLeaseRevokeRequestModel
{
    /// <summary>
    /// A note explaining the revocation. Recorded on the audit trail; not surfaced on the lease itself.
    /// </summary>
    public string? Reason { get; set; }
}
