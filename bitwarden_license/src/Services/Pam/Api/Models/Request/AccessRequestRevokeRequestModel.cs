namespace Bit.Services.Pam.Api.Models.Request;

/// <summary>
/// A request to revoke an access request that has not been activated.
/// </summary>
public class AccessRequestRevokeRequestModel
{
    /// <summary>
    /// Why the request is being revoked. Required when a managing approver revokes; recorded with their decision
    /// and surfaced to the requester.
    /// </summary>
    public string? Reason { get; set; }
}
