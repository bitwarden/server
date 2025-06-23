#nullable enable

namespace Bit.Core.Platform.MailDelivery;

internal class ClientCredentialsHandler : OAuthHandler
{
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

    protected override FormUrlEncodedContent BuildContent()
    {
        return new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "grant_type", "client_credentials" },
            { "client_id", _clientId },
            { "client_secret", _clientSecret },
            { "scope", _scope },
        });
    }
}
