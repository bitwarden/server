namespace Bit.Core.Pam.Models;

/// <summary>
/// The leasing context that governs a cipher for a particular caller: which collection's access rule applies, the
/// owning organization, and whether that rule requires human approval. A null resolution means the cipher is not
/// leasing-gated for the caller.
/// </summary>
public sealed record AccessApprovalResolution(
    Guid OrganizationId,
    Guid CollectionId,
    bool RequiresHumanApproval);
