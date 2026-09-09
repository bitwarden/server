#nullable enable

namespace Bit.Core.Models.Data;

/// <summary>
/// How a Send access should be attributed within a single organization's event log.
/// <para><see cref="AccessorUserId"/> is set when the accessor is a confirmed member of the org (their
/// platform user id, so the Admin Console member list resolves their name).</para>
/// <para><see cref="ClaimedDomain"/> is set when the accessor is not a member but their email domain is
/// one the org has claimed.</para>
/// When neither applies the org has no context entry and the access renders as "External".
/// </summary>
public record SendAccessEventOrgContext(Guid? AccessorUserId, string? ClaimedDomain);
