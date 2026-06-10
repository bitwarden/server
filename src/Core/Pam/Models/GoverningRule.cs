using Bit.Core.Pam.Models.Conditions;

namespace Bit.Core.Pam.Models;

/// <summary>
/// The access rule that governs a cipher for a particular caller: which collection's rule applies, the owning
/// organization, whether the rule requires human approval, and the parsed <see cref="AccessCondition"/> tree so the
/// rule engine can evaluate it against the caller's signals. A null governing rule means the cipher is not
/// leasing-gated for the caller.
/// </summary>
public sealed record GoverningRule(
    Guid OrganizationId,
    Guid CollectionId,
    bool RequiresHumanApproval,
    AccessCondition Condition);
