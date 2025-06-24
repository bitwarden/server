#nullable enable

namespace Bit.Core.Platform.MailDelivery;

internal class ClientCredentialsHandler : OAuthHandler
{
    public const string GrantType = "client_credentials";
    private readonly string _clientId;
    private readonly string _clientSecret;
    private readonly string _scope;

    public ClientCredentialsHandler(
        IHttpClientFactory httpClientFactory,
        TimeProvider timeProvider,
        string tokenEndpoint,
        string username,
        string clientId,
        string clientSecret,
        string scope)
        : base(httpClientFactory, timeProvider, tokenEndpoint, username)
    {
        _clientId = clientId;
        _clientSecret = clientSecret;
        _scope = scope;
    }

    protected override Dictionary<string, string> BuildContent()
    {
        return new Dictionary<string, string>
        {
            { "grant_type", GrantType },
            { "client_id", _clientId },
            { "client_secret", _clientSecret },
            { "scope", _scope },
        };
    }
}
