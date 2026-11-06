using Microsoft.Extensions.Logging;

namespace Bit.Core.Platform.Mail.Delivery;

// Handles the client_credentials grant for SMTP OAuth token exchange. Carries no
// provider-specific knowledge: callers resolve the token endpoint, scope, and credentials before
// constructing this handler.
internal sealed class ClientCredentialsHandler : OAuthHandler
{
    public const string GrantType = "client_credentials";

    private readonly string _clientId;
    private readonly string _clientSecret;
    private readonly string _scope;

    public ClientCredentialsHandler(
        IHttpClientFactory httpClientFactory,
        TimeProvider timeProvider,
        ILogger logger,
        string tokenEndpoint,
        string username,
        string clientId,
        string clientSecret,
        string scope)
        : base(httpClientFactory, timeProvider, logger, tokenEndpoint, username)
    {
        _clientId = clientId;
        _clientSecret = clientSecret;
        _scope = scope;
    }

    protected override Dictionary<string, string> BuildContent() => new()
    {
        ["grant_type"] = GrantType,
        ["client_id"] = _clientId,
        ["client_secret"] = _clientSecret,
        ["scope"] = _scope,
    };
}
