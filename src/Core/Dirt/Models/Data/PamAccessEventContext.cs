#nullable enable

using Bit.Core.Enums;

namespace Bit.Core.Models.Data;

/// <summary>
/// The organization-event-log projection of one PAM audit event. PAM's own append-only audit store stays the system of
/// record for the full trail; this carries just the facts <c>dbo.Event</c> can represent, so
/// <see cref="Bit.Core.Services.IEventService"/> does not have to depend on the PAM domain (and the PAM audit kinds do
/// not have to leak into Core).
/// </summary>
public record PamAccessEventContext
{
    public required Guid OrganizationId { get; init; }

    /// <summary>
    /// When the action occurred, as recorded by PAM — not when the fan-out ran. Passed through so the two trails agree
    /// on a timestamp even though the org event log is written after the fact.
    /// </summary>
    public required DateTime Date { get; init; }

    /// <summary>
    /// Who performed the action. Null for a system / automatic action (an automatic decision, a sweep), which sets
    /// <see cref="SystemUser"/> instead so the event log still names an actor.
    /// </summary>
    public Guid? ActingUserId { get; init; }

    /// <summary>The member the action was about — the access requester.</summary>
    public Guid? UserId { get; init; }

    /// <summary>
    /// The vault item the access concerns. Populated for every PAM event that reaches the organization event log, and
    /// the fact an administrator reading that log actually wants — the subject ids below are correlation handles into
    /// the PAM trail. Setting it also files the event under the item's own event history.
    /// </summary>
    public Guid? CipherId { get; init; }

    /// <summary>The gated collection the governing access rule belongs to.</summary>
    public Guid? CollectionId { get; init; }

    public Guid? AccessRequestId { get; init; }
    public Guid? AccessLeaseId { get; init; }

    /// <summary>Set in place of <see cref="ActingUserId"/> when PAM itself performed the action.</summary>
    public EventSystemUser? SystemUser { get; init; }
}
