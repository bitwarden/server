using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using MailKit.Security;
using Microsoft.Extensions.Logging;

namespace Bit.Core.Platform.Mail.Delivery;

// Base for SMTP OAuth token exchange: caches the access token (60s refresh skew), serializes
// concurrent fetches behind a semaphore, and supports compare-and-clear invalidation when the
// SMTP server rejects a cached token. Concrete grant types (e.g. client_credentials) implement
// BuildContent.
internal abstract class OAuthHandler
{
    internal const string HttpClientName = "SmtpOAuth";

    private static readonly TimeSpan _tokenRefreshSkew = TimeSpan.FromSeconds(60);

    private readonly HttpClient _httpClient;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger _logger;
    private readonly string _tokenEndpoint;
    private readonly string _username;

    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private string? _cachedAccessToken;
    private DateTime? _expirationTimeUtc;

    protected OAuthHandler(
        IHttpClientFactory httpClientFactory,
        TimeProvider timeProvider,
        ILogger logger,
        string tokenEndpoint,
        string username)
    {
        _httpClient = httpClientFactory.CreateClient(HttpClientName);
        _timeProvider = timeProvider;
        _logger = logger;
        _tokenEndpoint = tokenEndpoint;
        _username = username;
    }

    protected abstract Dictionary<string, string> BuildContent();

    /// <summary>
    /// Returns a SASL mechanism for the configured username, using the cached access token if
    /// it is still unexpired, otherwise fetching a fresh token from the configured endpoint.
    /// </summary>
    public async Task<SaslMechanism> GetAsync(CancellationToken cancellationToken)
    {
        await _semaphore.WaitAsync(cancellationToken);
        try
        {
            if (_expirationTimeUtc.HasValue && _timeProvider.GetUtcNow().UtcDateTime < _expirationTimeUtc.Value)
            {
                return new SaslMechanismOAuth2(_username, _cachedAccessToken!);
            }

            var token = await FetchTokenAsync(cancellationToken);
            _cachedAccessToken = token.AccessToken;
            // Without expires_in the token lifetime is unknown, so it is not reused.
            _expirationTimeUtc = token.ExpiresIn is { } expiresIn
                ? _timeProvider.GetUtcNow().UtcDateTime
                    .Add(TimeSpan.FromSeconds(expiresIn))
                    .Subtract(_tokenRefreshSkew)
                : null;

            return new SaslMechanismOAuth2(_username, token.AccessToken);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <summary>
    /// Clears the cached token if it still equals <paramref name="rejectedAccessToken"/>. If a
    /// concurrent refresh already replaced the cached token, the cache is left untouched.
    /// </summary>
    public async Task InvalidateIfCurrentAsync(string rejectedAccessToken, CancellationToken cancellationToken)
    {
        await _semaphore.WaitAsync(cancellationToken);
        try
        {
            if (_cachedAccessToken == rejectedAccessToken)
            {
                _cachedAccessToken = null;
                _expirationTimeUtc = null;
            }
        }
        finally
        {
            _semaphore.Release();
        }
    }

    private async Task<TokenSuccessResponse> FetchTokenAsync(CancellationToken cancellationToken)
    {
        using var response = await _httpClient.PostAsync(
            _tokenEndpoint, new FormUrlEncodedContent(BuildContent()), cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var error = await TryReadJsonAsync<TokenErrorResponse>(response, cancellationToken);

            _logger.LogWarning(
                "SMTP OAuth token request to {TokenEndpointHost} failed with {StatusCode}: {Error} {ErrorDescription}",
                SafeHost(_tokenEndpoint),
                response.StatusCode,
                error?.Error ?? "(non-JSON response)",
                error?.ErrorDescription ?? "(no description)");

            throw new TokenException(SafeHost(_tokenEndpoint), response.StatusCode, error);
        }

        var token = await TryReadJsonAsync<TokenSuccessResponse>(response, cancellationToken);
        return token ?? throw new TokenException(SafeHost(_tokenEndpoint), response.StatusCode, tokenErrorResponse: null);
    }

    private static async Task<T?> TryReadJsonAsync<T>(HttpResponseMessage response, CancellationToken cancellationToken)
        where T : class
    {
        try
        {
            return await response.Content.ReadFromJsonAsync<T>(cancellationToken);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string SafeHost(string tokenEndpoint)
        => Uri.TryCreate(tokenEndpoint, UriKind.Absolute, out var uri) ? uri.Host : tokenEndpoint;
}

// System.Text.Json treats `required` as "must be present in the JSON" and throws JsonException
// otherwise, so only fields RFC 6749 requires are marked required.
// https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/required-properties
internal sealed record TokenSuccessResponse
{
    [JsonPropertyName("access_token")]
    public required string AccessToken { get; init; }

    // RECOMMENDED, not required: https://www.rfc-editor.org/rfc/rfc6749#section-5.1
    // Microsoft always returns it, but other providers might not:
    // https://learn.microsoft.com/en-us/entra/identity-platform/v2-oauth2-client-creds-grant-flow
    [JsonPropertyName("expires_in")]
    public int? ExpiresIn { get; init; }

    // Overrides the generated record ToString, which prints every property, to keep the bearer
    // token out of logs.
    public override string ToString() =>
        $"{nameof(TokenSuccessResponse)} {{ AccessToken = [redacted], ExpiresIn = {ExpiresIn} }}";
}

internal sealed record TokenErrorResponse
{
    [JsonPropertyName("error")]
    public required string Error { get; init; }

    // OPTIONAL: https://www.rfc-editor.org/rfc/rfc6749#section-5.2
    // Microsoft always returns it, but other providers might not:
    // https://learn.microsoft.com/en-us/entra/identity-platform/v2-oauth2-client-creds-grant-flow
    [JsonPropertyName("error_description")]
    public string? ErrorDescription { get; init; }
}

internal sealed class TokenException : Exception
{
    public TokenException(string tokenEndpointHost, HttpStatusCode statusCode, TokenErrorResponse? tokenErrorResponse)
        : base(BuildMessage(tokenEndpointHost, statusCode, tokenErrorResponse))
    {
        TokenEndpointHost = tokenEndpointHost;
        StatusCode = statusCode;
    }

    public string TokenEndpointHost { get; }
    public HttpStatusCode StatusCode { get; }

    private static string BuildMessage(string tokenEndpointHost, HttpStatusCode statusCode, TokenErrorResponse? tokenErrorResponse)
        => tokenErrorResponse switch
        {
            null => $"Error during request to {tokenEndpointHost} ({statusCode}). The response body was not a recognizable error payload.",
            { ErrorDescription: null } => $"Error during request to {tokenEndpointHost} ({statusCode}). Encountered error {tokenErrorResponse.Error}.",
            _ => $"Error during request to {tokenEndpointHost} ({statusCode}). Encountered error {tokenErrorResponse.Error} with description '{tokenErrorResponse.ErrorDescription}'",
        };
}
