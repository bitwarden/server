#nullable enable

using Bit.Core.Enums;

namespace Bit.Core.Models.Data;

/// <summary>
/// The organization-event-log projection of a PAM audit event, carrying only what <c>dbo.Event</c> can represent so
/// <see cref="Bit.Core.Services.IEventService"/> need not depend on the PAM domain.
/// </summary>
public record PamAccessEventContext
{
    public required Guid OrganizationId { get; init; }

    /// <summary>The action's own timestamp as recorded by PAM, so the two trails agree.</summary>
    public required DateTime Date { get; init; }

    /// <summary>
    /// Null for an automatic action, which sets <see cref="SystemUser"/> instead so the event log still names an actor.
    /// </summary>
    public Guid? ActingUserId { get; init; }

    /// <summary>The access requester.</summary>
    public Guid? UserId { get; init; }

    /// <summary>
    /// Also files the event under the item's own history; null on rule events. The ids below are correlation handles
    /// into the PAM trail.
    /// </summary>
    public Guid? CipherId { get; init; }

    /// <summary>The gated collection the governing access rule belongs to.</summary>
    public Guid? CollectionId { get; init; }

    public Guid? AccessRequestId { get; init; }
    public Guid? AccessLeaseId { get; init; }

    public EventSystemUser? SystemUser { get; init; }
}
