namespace Bit.Services.Pam.Api.Models;

/// <summary>
/// The approval path a lease request will take, as it appears on the wire: <c>0 = automatic</c> (the client collects
/// a duration), <c>1 = human</c> (the client collects a window and a justification). Surfaced by the pre-check so
/// the client can present the right workflow.
/// </summary>
/// <remarks>
/// Scaffold only: a standalone copy of the domain approval mode so the DTOs carry the wire contract without coupling
/// to the PAM domain. The real type lands with the rest of the PAM feature.
/// </remarks>
public enum AccessApprovalMode : byte
{
    /// <summary>The request will be decided automatically; the client collects a duration.</summary>
    Automatic = 0,

    /// <summary>The request needs an approver; the client collects a window and a justification.</summary>
    Human = 1,
}
