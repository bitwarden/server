#nullable enable

using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using MailKit.Security;

namespace Bit.Core.Platform.MailDelivery;

internal abstract class OAuthHandler
{
    internal const string HttpClientName = "SmtpOAuth";

    private static readonly TimeSpan _tokenRefreshSkew = TimeSpan.FromSeconds(60);
    private readonly TimeProvider _timeProvider;
    private readonly string _tokenEndpoint;
    private readonly string _username;
    private readonly HttpClient _httpClient;

    private readonly SemaphoreSlim _semaphore = new(1);

    private string? _cachedAccessToken = null;
    private DateTime? _expirationTime = null;

    public OAuthHandler(IHttpClientFactory httpClientFactory, TimeProvider timeProvider, string tokenEndpoint, string username)
    {
        _httpClient = httpClientFactory.CreateClient(HttpClientName);
        _timeProvider = timeProvider;
        _tokenEndpoint = tokenEndpoint;
        _username = username;
    }

    protected abstract FormUrlEncodedContent BuildContent();

    public async Task<SaslMechanism?> GetAsync(CancellationToken cancellationToken)
    {
        await _semaphore.WaitAsync(cancellationToken);

        try
        {
            if (_expirationTime.HasValue && _timeProvider.GetUtcNow().UtcDateTime < _expirationTime.Value)
            {
                Debug.Assert(_cachedAccessToken is not null);
                return new SaslMechanismOAuth2(_username, _cachedAccessToken);
            }

            var content = BuildContent();

            var response = await _httpClient.PostAsync(_tokenEndpoint, content, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadFromJsonAsync<TokenErrorResponse>(cancellationToken)
                    // Should never happen
                    ?? throw new HttpRequestException($"Response indicated a problem ({response.StatusCode}) but the response content was a null JSON literal.");

                throw new TokenException(_tokenEndpoint, response, error);
            }

            var tokenResponse = await response.Content.ReadFromJsonAsync<TokenSuccessResponse>(cancellationToken)
                // Should never happen
                ?? throw new HttpRequestException("Response indicated success but the response content was a null JSON literal.");

            // TODO: Should we have some way to bust cache if the token fails to send an email?
            _cachedAccessToken = tokenResponse.AccessToken;
            _expirationTime = _timeProvider.GetUtcNow().UtcDateTime.Add(TimeSpan.FromSeconds(tokenResponse.ExpiresIn).Subtract(_tokenRefreshSkew));

            return new SaslMechanismOAuth2(_username, tokenResponse.AccessToken);
        }
        catch
        {
            _cachedAccessToken = null;
            _expirationTime = null;
            // TODO: Log error
            throw;
        }
        finally
        {
            _semaphore.Release();
        }
    }
}

public record TokenSuccessResponse
{
    [JsonPropertyName("access_token")]
    public required string AccessToken { get; init; }
    [JsonPropertyName("expires_in")]
    public required int ExpiresIn { get; init; }
}

public record TokenErrorResponse
{
    [JsonPropertyName("error")]
    public required string Error { get; init; }
    [JsonPropertyName("error_description")]
    public required string ErrorDescription { get; init; }
    [JsonExtensionData]
    public Dictionary<string, JsonElement> Extensions { get; set; } = [];
}

public class TokenException : Exception
{
    public TokenException(string tokenEndpoint, HttpResponseMessage response, TokenErrorResponse tokenErrorResponse)
        : base($"Error during request to {tokenEndpoint} ({response.StatusCode}). Encountered error {tokenErrorResponse.Error} with description '{tokenErrorResponse.ErrorDescription}'")
    {
        TokenEndpoint = tokenEndpoint;
        Response = response;
        TokenErrorResponse = tokenErrorResponse;
    }

    public string TokenEndpoint { get; }
    public HttpResponseMessage Response { get; }
    public TokenErrorResponse TokenErrorResponse { get; }
}
