using Bit.Core.Pam.Models.Rules;

namespace Bit.Core.Pam.Models;

/// <summary>
/// The leasing context that governs a cipher for a particular caller: which collection's access rule applies, the
/// owning organization, whether that rule requires human approval, and the parsed <see cref="Rule"/> itself so the
/// policy engine can evaluate it against the caller's signals. A null resolution means the cipher is not
/// leasing-gated for the caller.
/// </summary>
public sealed record AccessApprovalResolution(
    Guid OrganizationId,
    Guid CollectionId,
    bool RequiresHumanApproval,
    Rule Rule);
