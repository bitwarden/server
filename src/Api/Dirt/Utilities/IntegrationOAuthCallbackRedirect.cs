using Bit.Core.Dirt.Enums;
using Bit.Core.Settings;

namespace Bit.Api.Dirt.Utilities;

/// <summary>
/// Builds the web vault URL that an OAuth-based integration's callback sends the browser back to, so the
/// organization owner lands on the Event management page instead of a raw API response.
/// </summary>
public static class IntegrationOAuthCallbackRedirect
{
    public const string Success = "success";

    /// <summary>The user declined consent, or the provider returned an error instead of a code.</summary>
    public const string Canceled = "canceled";

    /// <summary>The token exchange or a follow-up provider call failed.</summary>
    public const string Failed = "failed";

    /// <summary>The signed-in Microsoft user isn't a member of any team.</summary>
    public const string NoTeams = "noTeams";

    /// <summary>The integration was already connected, e.g. the callback was opened a second time.</summary>
    public const string AlreadyConnected = "alreadyConnected";

    public static string BuildUrl(
        IGlobalSettings globalSettings,
        Guid organizationId,
        IntegrationType integrationType,
        string result)
    {
        var integration = integrationType.ToString().ToLowerInvariant();
        return $"{globalSettings.BaseServiceUri.VaultWithHash}/organizations/{organizationId}" +
               $"/integrations/event-management?integration={integration}&result={result}";
    }
}
