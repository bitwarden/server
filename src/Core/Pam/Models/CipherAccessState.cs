using Bit.Core.Pam.Entities;

namespace Bit.Core.Pam.Models;

/// <summary>
/// The caller's access state for a single cipher: the active lease they hold (if any) and their pending request (if
/// any). The approved-but-not-yet-activated request the client models has no server counterpart in v0 — approval
/// mints an active lease immediately — so it is always absent here.
/// </summary>
public record CipherAccessState(Guid CipherId, AccessLease? ActiveLease, AccessRequestDetails? PendingRequest);
