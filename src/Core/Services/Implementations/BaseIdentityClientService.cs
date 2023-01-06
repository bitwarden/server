using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Bit.Core.Utilities;
using Microsoft.Extensions.Logging;

namespace Bit.Core.Services;

public abstract class BaseIdentityClientService : IDisposable
{
    private readonly IHttpClientFactory _httpFactory;
    private readonly string _identityScope;
    private readonly string _identityClientId;
    private readonly string _identityClientSecret;
    protected readonly ILogger<BaseIdentityClientService> _logger;

    private JsonDocument _decodedToken;
    private DateTime? _nextAuthAttempt = null;

    public BaseIdentityClientService(
        IHttpClientFactory httpFactory,
        string baseClientServerUri,
        string baseIdentityServerUri,
        string identityScope,
        string identityClientId,
        string identityClientSecret,
        ILogger<BaseIdentityClientService> logger)
    {
        _httpFactory = httpFactory;
        _identityScope = identityScope;
        _identityClientId = identityClientId;
        _identityClientSecret = identityClientSecret;
        _logger = logger;

        Client = _httpFactory.CreateClient("client");
        Client.BaseAddress = new Uri(baseClientServerUri);
        Client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        IdentityClient = _httpFactory.CreateClient("identity");
        IdentityClient.BaseAddress = new Uri(baseIdentityServerUri);
        IdentityClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }

    protected HttpClient Client { get; private set; }
    protected HttpClient IdentityClient { get; private set; }
    protected string AccessToken { get; private set; }

    protected Task SendAsync(HttpMethod method, string path) =>
        SendAsync<object>(method, path, null);

    protected Task SendAsync<TRequest>(HttpMethod method, string path, TRequest requestModel) =>
        SendAsync<TRequest, object>(method, path, requestModel, false);

    protected async Task<TResult> SendAsync<TRequest, TResult>(HttpMethod method, string path,
        TRequest requestModel, bool hasJsonResult)
    {
        var fullRequestPath = string.Concat(Client.BaseAddress, path);

        var tokenStateResponse = await HandleTokenStateAsync();
        if (!tokenStateResponse)
        {
            _logger.LogError("Unable to send {method} request to {requestUri} because an access token was unable to be obtained",
                method.Method, fullRequestPath);
            return default;
        }

        var message = new TokenHttpRequestMessage(requestModel, AccessToken)
        {
            Method = method,
            RequestUri = new Uri(fullRequestPath)
        };
        try
        {
            var response = await Client.SendAsync(message);
            if (response.IsSuccessStatusCode)
            {
                if (hasJsonResult)
                {
                    return await response.Content.ReadFromJsonAsync<TResult>();
                }
            }
            else
            {
                _logger.LogError("Request to {url} is unsuccessful with status of {code}-{reason}",
                    message.RequestUri.ToString(), response.StatusCode, response.ReasonPhrase);
            }
            return default;
        }
        catch (Exception e)
        {
            _logger.LogError(12334, e, "Failed to send to {0}.", message.RequestUri.ToString());
            return default;
        }
    }

    protected async Task<bool> HandleTokenStateAsync()
    {
        if (_nextAuthAttempt.HasValue && DateTime.UtcNow < _nextAuthAttempt.Value)
        {
            _logger.LogInformation("Not requesting a token at {now} because the next request time is {nextAttempt}", DateTime.UtcNow, _nextAuthAttempt.Value);
            return false;
        }
        _nextAuthAttempt = null;

        if (!string.IsNullOrWhiteSpace(AccessToken) && !TokenNeedsRefresh())
        {
            return true;
        }

        var requestMessage = new HttpRequestMessage
        {
            Method = HttpMethod.Post,
            RequestUri = new Uri(string.Concat(IdentityClient.BaseAddress, "connect/token")),
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                { "grant_type", "client_credentials" },
                { "scope", _identityScope },
                { "client_id", _identityClientId },
                { "client_secret", _identityClientSecret }
            })
        };

        HttpResponseMessage response = null;
        try
        {
            response = await IdentityClient.SendAsync(requestMessage);
        }
        catch (Exception e)
        {
            _logger.LogError(12339, e, "Unable to authenticate with identity server.");
        }

        if (response == null)
        {
            _logger.LogError("Empty token response from {identity} for client {clientId}", IdentityClient.BaseAddress, _identityClientId);
            return false;
        }

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Unsuccessful token response from {identity} for client {clientId} with status {code}-{reason}", IdentityClient.BaseAddress, _identityClientId, response.StatusCode, response.ReasonPhrase);

            if (response.StatusCode == HttpStatusCode.BadRequest)
            {
                _nextAuthAttempt = DateTime.UtcNow.AddDays(1);
            }

            if (_logger.IsEnabled(LogLevel.Debug))
            {
                var responseBody = await response.Content.ReadAsStringAsync();
                _logger.LogDebug("Error response body:\n{ResponseBody}", responseBody);
            }

            return false;
        }

        var content = await response.Content.ReadAsStreamAsync();
        using var jsonDocument = await JsonDocument.ParseAsync(content);

        AccessToken = jsonDocument.RootElement.GetProperty("access_token").GetString();
        return true;
    }

    protected class TokenHttpRequestMessage : HttpRequestMessage
    {
        public TokenHttpRequestMessage(string token)
        {
            Headers.Add("Authorization", $"Bearer {token}");
        }

        public TokenHttpRequestMessage(object requestObject, string token)
            : this(token)
        {
            if (requestObject != null)
            {
                Content = JsonContent.Create(requestObject);
            }
        }
    }

    protected bool TokenNeedsRefresh(int minutes = 5)
    {
        var decoded = DecodeToken();
        if (!decoded.RootElement.TryGetProperty("exp", out var expProp))
        {
            throw new InvalidOperationException("No exp in token.");
        }

        var expiration = CoreHelpers.FromEpocSeconds(expProp.GetInt64());
        return DateTime.UtcNow.AddMinutes(-1 * minutes) > expiration;
    }

    protected JsonDocument DecodeToken()
    {
        if (_decodedToken != null)
        {
            return _decodedToken;
        }

        if (AccessToken == null)
        {
            throw new InvalidOperationException($"{nameof(AccessToken)} not found.");
        }

        var parts = AccessToken.Split('.');
        if (parts.Length != 3)
        {
            throw new InvalidOperationException($"{nameof(AccessToken)} must have 3 parts");
        }

        var decodedBytes = CoreHelpers.Base64UrlDecode(parts[1]);
        if (decodedBytes == null || decodedBytes.Length < 1)
        {
            throw new InvalidOperationException($"{nameof(AccessToken)} must have 3 parts");
        }

        _decodedToken = JsonDocument.Parse(decodedBytes);
        return _decodedToken;
    }

    public void Dispose()
    {
        _decodedToken?.Dispose();
    }
}
