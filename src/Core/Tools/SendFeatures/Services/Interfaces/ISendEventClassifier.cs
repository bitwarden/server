#nullable enable

using Bit.Core.Models.Data;

namespace Bit.Core.Tools.SendFeatures.Services.Interfaces;

/// <summary>
/// Builds the per-organization attribution context for a Send access event. For each of the Send
/// owner's organizations the accessor is meaningful to, the returned dictionary carries a
/// <see cref="SendAccessEventOrgContext"/> — a confirmed member is attributed via its
/// <c>AccessorUserId</c>; an accessor whose email domain matches one of the org's claimed
/// <see cref="Bit.Core.Entities.OrganizationDomain"/>s via its <c>ClaimedDomain</c>. Organizations
/// where the accessor is neither are omitted (the access renders as "External"). Pass the result to
/// <see cref="Bit.Core.Services.IEventService.LogSendEventAsync"/>.
/// </summary>
public interface ISendEventClassifier
{
    /// <summary>
    /// Build the per-organization access context for a Send owned by <paramref name="sendOwnerUserId"/>.
    /// <paramref name="accessorEmail"/> is the email-verification accessor's address; pass <c>null</c>
    /// for password/none/anonymous access (returns an empty dictionary — every org is External).
    /// </summary>
    Task<IReadOnlyDictionary<Guid, SendAccessEventOrgContext>> BuildAccessContextAsync(
        Guid sendOwnerUserId,
        string? accessorEmail);
}
