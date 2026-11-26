namespace Bit.AppHost.Configuration;

/// <summary>
/// A service of the Bitwarden server monolith that the AppHost runs.
/// </summary>
/// <remarks>
/// Bitwarden is moving from the monolith to independently deployed services. The members are the monolith's own
/// hosts, built and released from this repository; a service extracted from the monolith is not a member.
/// </remarks>
internal enum MonolithService
{
    /// <summary>
    /// The admin portal, <c>src/Admin</c>.
    /// </summary>
    Admin,

    /// <summary>
    /// The main API, <c>src/Api</c>.
    /// </summary>
    Api,

    /// <summary>
    /// Billing webhooks and jobs, <c>src/Billing</c>.
    /// </summary>
    Billing,

    /// <summary>
    /// Event ingestion, <c>src/Events</c>.
    /// </summary>
    Events,

    /// <summary>
    /// The event queue processor, <c>src/EventsProcessor</c>.
    /// </summary>
    EventsProcessor,

    /// <summary>
    /// Website icon fetching, <c>src/Icons</c>.
    /// </summary>
    Icons,

    /// <summary>
    /// The identity server, <c>src/Identity</c>.
    /// </summary>
    Identity,

    /// <summary>
    /// Push notifications over SignalR, <c>src/Notifications</c>.
    /// </summary>
    Notifications,

    /// <summary>
    /// SCIM provisioning, <c>bitwarden_license/src/Scim</c>.
    /// </summary>
    Scim,

    /// <summary>
    /// SSO, <c>bitwarden_license/src/Sso</c>.
    /// </summary>
    Sso
}
