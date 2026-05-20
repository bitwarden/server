#nullable enable

using Bit.Core.Enums;

namespace Bit.Core.Tools.SendFeatures.Services.Interfaces;

/// <summary>
/// Builds per-organization <see cref="EventType"/> resolvers that classify Send activity as
/// originating inside (claimed-domain) or outside (external-domain) an organization's claimed
/// <see cref="Bit.Core.Entities.OrganizationDomain"/>s. Pass the returned delegate to
/// <see cref="Bit.Core.Services.IEventService.LogUserEventAsync"/>'s per-organization resolver.
/// </summary>
public interface ISendEventClassifier
{
    /// <summary>
    /// Build a resolver that classifies a single accessor's email against each of the Send
    /// owner's organizations' claimed domains. Returns <c>null</c> when no classification is
    /// possible (no accessor email, no claimed domains).
    /// </summary>
    Task<Func<Guid, EventType?>?> BuildAccessResolverAsync(
        Guid sendOwnerUserId,
        string? accessorEmail,
        EventType claimedDomainVariant,
        EventType externalDomainVariant);

    /// <summary>
    /// Build a resolver that classifies a comma-separated list of recipient emails (the
    /// <see cref="Bit.Core.Tools.Entities.Send.Emails"/> field) against each of the Send
    /// owner's organizations' claimed domains. The org is classified as claimed only when
    /// every recipient domain is inside that org's claimed-domain set; otherwise external.
    /// </summary>
    Task<Func<Guid, EventType?>?> BuildCreationResolverAsync(
        Guid sendOwnerUserId,
        string? recipientEmails,
        EventType claimedDomainVariant,
        EventType externalDomainVariant);
}
