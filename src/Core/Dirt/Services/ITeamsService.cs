using Bit.Core.Dirt.Models.Data.Teams;
using Bit.Core.Dirt.Services.Implementations;

namespace Bit.Core.Dirt.Services;

/// <summary>
/// Service that provides functionality relating to the Microsoft Teams integration including OAuth,
/// team discovery and sending a message to a channel in Teams.
/// </summary>
public interface ITeamsService
{
    /// <summary>
    /// Generate the Microsoft Teams OAuth 2.0 authorization URL used to begin the sign-in flow.
    /// </summary>
    /// <param name="callbackUrl">The absolute redirect URI that Microsoft will call after user authorization.
    /// Must match the URI registered with the app configuration.</param>
    /// <param name="state">A state token used to correlate the request and callback and prevent CSRF attacks.</param>
    /// <returns>The full authorization URL to which the user should be redirected to begin the sign-in process.</returns>
    string GetRedirectUrl(string callbackUrl, string state);

    /// <summary>
    /// Exchange the OAuth code for a Microsoft Graph API access token.
    /// </summary>
    /// <param name="code">The code returned from Microsoft via the OAuth callback Url.</param>
    /// <param name="redirectUrl">The same redirect URI that was passed to the authorization request.</param>
    /// <returns>A valid Microsoft Graph access token if the exchange succeeds; otherwise, an empty string.</returns>
    Task<string> ObtainTokenViaOAuth(string code, string redirectUrl);

    /// <summary>
    /// Get the Teams to which the authenticated user belongs via Microsoft Graph API.
    /// </summary>
    /// <param name="accessToken">A valid Microsoft Graph access token for the user (obtained via OAuth).</param>
    /// <returns>A read-only list of <see cref="TeamInfo"/> objects representing the user’s joined teams.
    /// Returns an empty list if the request fails or if the token is invalid.</returns>
    Task<IReadOnlyList<TeamInfo>> GetJoinedTeamsAsync(string accessToken);

    /// <summary>
    /// Send a message to a specific channel in Teams.
    /// </summary>
    /// <remarks>This is used primarily by the <see cref="TeamsIntegrationHandler"/> to send events to the
    /// Teams channel.</remarks>
    /// <param name="serviceUri">The service URI associated with the Microsoft Bot Framework connector for the target
    /// team. Obtained via the bot framework callback.</param>
    /// <param name="channelId"> The conversation or channel ID where the message should be delivered. Obtained via
    /// the bot framework callback.</param>
    /// <param name="message">The message text to post to the channel.</param>
    /// <returns>A task that completes when the message has been sent. Errors during message delivery are surfaced
    /// as exceptions from the underlying connector client.</returns>
    Task SendMessageToChannelAsync(Uri serviceUri, string channelId, string message);
}
