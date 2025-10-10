namespace Bit.Core.Services;

/// <summary>Defines operations for interacting with Slack, including OAuth authentication, channel discovery,
/// and sending messages.</summary>
public interface ISlackService
{
    /// <remarks>Note: This API is not currently used (yet) by any server code. It is here to provide functionality if
    /// the UI needs to be able to look up channels for a user.</remarks>
    /// <summary>Retrieves the ID of a Slack channel by name.
    /// See <see href="https://api.slack.com/methods/conversations.list">conversations.list API</see>.</summary>
    /// <param name="token">A valid Slack OAuth access token.</param>
    /// <param name="channelName">The name of the channel to look up.</param>
    /// <returns>The channel ID if found; otherwise, an empty string.</returns>
    Task<string> GetChannelIdAsync(string token, string channelName);

    /// <remarks>Note: This API is not currently used (yet) by any server code. It is here to provide functionality if
    /// the UI needs to be able to look up channels for a user.</remarks>
    /// <summary>Retrieves the IDs of multiple Slack channels by name.
    /// See <see href="https://api.slack.com/methods/conversations.list">conversations.list API</see>.</summary>
    /// <param name="token">A valid Slack OAuth access token.</param>
    /// <param name="channelNames">A list of channel names to look up.</param>
    /// <returns>A list of matching channel IDs. Channels that cannot be found are omitted.</returns>
    Task<List<string>> GetChannelIdsAsync(string token, List<string> channelNames);

    /// <remarks>Note: This API is not currently used (yet) by any server code. It is here to provide functionality if
    /// the UI needs to be able to look up a user by their email address.</remarks>
    /// <summary>Retrieves the DM channel ID for a Slack user by email.
    /// See <see href="https://api.slack.com/methods/users.lookupByEmail">users.lookupByEmail API</see> and
    /// <see href="https://api.slack.com/methods/conversations.open">conversations.open API</see>.</summary>
    /// <param name="token">A valid Slack OAuth access token.</param>
    /// <param name="email">The email address of the user to open a DM with.</param>
    /// <returns>The DM channel ID if successful; otherwise, an empty string.</returns>
    Task<string> GetDmChannelByEmailAsync(string token, string email);

    /// <summary>Builds the Slack OAuth 2.0 authorization URL for the app.
    /// See <see href="https://api.slack.com/authentication/oauth-v2">Slack OAuth v2 documentation</see>.</summary>
    /// <param name="callbackUrl">The absolute redirect URI that Slack will call after user authorization.
    /// Must match the URI registered with the app configuration.</param>
    /// <param name="state">A state token used to correlate the request and callback and prevent CSRF attacks.</param>
    /// <returns>The full authorization URL to which the user should be redirected to begin the sign-in process.</returns>
    string GetRedirectUrl(string callbackUrl, string state);

    /// <summary>Exchanges a Slack OAuth code for an access token.
    /// See <see href="https://api.slack.com/methods/oauth.v2.access">oauth.v2.access API</see>.</summary>
    /// <param name="code">The authorization code returned by Slack via the callback URL after user authorization.</param>
    /// <param name="redirectUrl">The redirect URI that was used in the authorization request.</param>
    /// <returns>A valid Slack access token if successful; otherwise, an empty string.</returns>
    Task<string> ObtainTokenViaOAuth(string code, string redirectUrl);

    /// <summary>Sends a message to a Slack channel by ID.
    /// See <see href="https://api.slack.com/methods/chat.postMessage">chat.postMessage API</see>.</summary>
    /// <remarks>This is used primarily by the <see cref="SlackIntegrationHandler"/> to send events to the
    /// Slack channel.</remarks>
    /// <param name="token">A valid Slack OAuth access token.</param>
    /// <param name="message">The message text to send.</param>
    /// <param name="channelId">The channel ID to send the message to.</param>
    /// <returns>A task that completes when the message has been sent.</returns>
    Task SendSlackMessageByChannelIdAsync(string token, string message, string channelId);
}
