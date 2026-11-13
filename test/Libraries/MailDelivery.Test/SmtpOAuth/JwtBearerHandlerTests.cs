using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Bit.Core.Platform.Mail.Delivery;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using NSubstitute;
using RichardSzalay.MockHttp;

namespace Bit.MailDelivery.Test.SmtpOAuth;

public class JwtBearerHandlerTests : IDisposable
{
    private const string TokenEndpoint = "https://example.com/token";
    private const string OutboundMailbox = "sender@example.com";

    private readonly MockHttpMessageHandler _mockHandler;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly FakeTimeProvider _fakeTimeProvider;
    private readonly RSA _rsa;
    private readonly string _signingKeyPem;
    private readonly RsaSecurityKey _publicKey;

    private Dictionary<string, string>? _capturedFormData;

    public JwtBearerHandlerTests()
    {
        _mockHandler = new MockHttpMessageHandler();
        _httpClientFactory = Substitute.For<IHttpClientFactory>();
        _httpClientFactory.CreateClient("SmtpOAuth").Returns(_mockHandler.ToHttpClient());

        _fakeTimeProvider = new FakeTimeProvider(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));

        _rsa = RSA.Create(2048);
        _signingKeyPem = _rsa.ExportPkcs8PrivateKeyPem();
        _publicKey = new RsaSecurityKey(_rsa.ExportParameters(includePrivateParameters: false));
    }

    public void Dispose() => _rsa.Dispose();

    private JwtBearerHandler CreateHandler(IReadOnlyDictionary<string, string> claims) => new(
        _httpClientFactory,
        _fakeTimeProvider,
        Substitute.For<ILogger<JwtBearerHandler>>(),
        TokenEndpoint,
        OutboundMailbox,
        _signingKeyPem,
        claims);

    // Captures the posted form body and responds with a bare access token. Passing expiresIn: null
    // omits expires_in from the response, matching OAuthHandler's "unknown lifetime" path so the
    // cached token is never reused and every GetAsync call exercises BuildContent again.
    private void RespondCapturingRequest(int? expiresIn)
    {
        _mockHandler.When(HttpMethod.Post, TokenEndpoint).Respond(async req =>
        {
            var body = await req.Content!.ReadAsStringAsync();
            _capturedFormData = ParseFormUrlEncoded(body);

            var payload = new Dictionary<string, object>
            {
                ["access_token"] = "test_token",
            };
            if (expiresIn is { } value)
            {
                payload["expires_in"] = value;
            }

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json"),
            };
        });
    }

    private static Dictionary<string, string> ParseFormUrlEncoded(string encoded)
    {
        var result = new Dictionary<string, string>();
        foreach (var pair in encoded.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = pair.Split('=', 2);
            result[Uri.UnescapeDataString(parts[0])] = Uri.UnescapeDataString(parts[1]);
        }

        return result;
    }

    private async Task<JsonWebToken> ValidateAsync(string assertion, RsaSecurityKey? signingKey = null)
    {
        var tokenHandler = new JsonWebTokenHandler();
        var result = await tokenHandler.ValidateTokenAsync(assertion, new TokenValidationParameters
        {
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = false,
            IssuerSigningKey = signingKey ?? _publicKey,
        });

        Assert.True(result.IsValid, result.Exception?.ToString());
        return (JsonWebToken)result.SecurityToken;
    }

    [Fact]
    public async Task PostsOnlyGrantTypeAndAssertion()
    {
        RespondCapturingRequest(expiresIn: null);
        var handler = CreateHandler(new Dictionary<string, string>());

        await handler.GetAsync(CancellationToken.None);

        Assert.NotNull(_capturedFormData);
        Assert.Equal(2, _capturedFormData!.Count);
        Assert.Equal(JwtBearerHandler.GrantType, _capturedFormData["grant_type"]);
        Assert.False(string.IsNullOrEmpty(_capturedFormData["assertion"]));
    }

    [Fact]
    public async Task AssertionContainsConfiguredClaimsAndTimes()
    {
        RespondCapturingRequest(expiresIn: null);
        var handler = CreateHandler(new Dictionary<string, string>
        {
            ["iss"] = "service-account@example.iam.gserviceaccount.com",
            ["aud"] = "https://oauth2.googleapis.com/token",
            ["scope"] = "https://mail.google.com/",
        });

        await handler.GetAsync(CancellationToken.None);

        var token = await ValidateAsync(_capturedFormData!["assertion"]);
        var now = _fakeTimeProvider.GetUtcNow().UtcDateTime;

        Assert.Equal(SecurityAlgorithms.RsaSha256, token.Alg);
        Assert.Equal("service-account@example.iam.gserviceaccount.com", token.GetClaim("iss").Value);
        Assert.Equal("https://oauth2.googleapis.com/token", token.GetClaim("aud").Value);
        Assert.Equal("https://mail.google.com/", token.GetClaim("scope").Value);
        Assert.Equal(now, token.IssuedAt);
        Assert.Equal(now, token.ValidFrom);
        Assert.Equal(now.AddMinutes(5), token.ValidTo);
    }

    [Fact]
    public async Task SubDefaultsToOutboundMailboxWhenNotConfigured()
    {
        RespondCapturingRequest(expiresIn: null);
        var handler = CreateHandler(new Dictionary<string, string>());

        await handler.GetAsync(CancellationToken.None);

        var token = await ValidateAsync(_capturedFormData!["assertion"]);
        Assert.Equal(OutboundMailbox, token.GetClaim("sub").Value);
    }

    [Fact]
    public async Task ConfiguredSubOverridesOutboundMailbox()
    {
        RespondCapturingRequest(expiresIn: null);
        var handler = CreateHandler(new Dictionary<string, string> { ["sub"] = "impersonated@example.com" });

        await handler.GetAsync(CancellationToken.None);

        var token = await ValidateAsync(_capturedFormData!["assertion"]);
        Assert.Equal("impersonated@example.com", token.GetClaim("sub").Value);
    }

    [Fact]
    public async Task EmptyValuedConfiguredClaimsAreOmitted()
    {
        RespondCapturingRequest(expiresIn: null);
        var handler = CreateHandler(new Dictionary<string, string>
        {
            ["aud"] = "",
            ["scope"] = "https://mail.google.com/",
        });

        await handler.GetAsync(CancellationToken.None);

        var token = await ValidateAsync(_capturedFormData!["assertion"]);
        Assert.False(token.TryGetClaim("aud", out _));
        Assert.Equal("https://mail.google.com/", token.GetClaim("scope").Value);
    }

    [Fact]
    public async Task TwoConsecutiveFetchesBothSucceedAndVerify()
    {
        RespondCapturingRequest(expiresIn: null);
        var handler = CreateHandler(new Dictionary<string, string> { ["iss"] = "svc@example.com" });

        await handler.GetAsync(CancellationToken.None);
        var firstAssertion = _capturedFormData!["assertion"];

        await handler.GetAsync(CancellationToken.None);
        var secondAssertion = _capturedFormData!["assertion"];

        await ValidateAsync(firstAssertion);
        await ValidateAsync(secondAssertion);
    }

    [Fact]
    public async Task SignatureValidationFailsAgainstDifferentKey()
    {
        RespondCapturingRequest(expiresIn: null);
        var handler = CreateHandler(new Dictionary<string, string>());

        await handler.GetAsync(CancellationToken.None);

        using var otherRsa = RSA.Create(2048);
        var otherPublicKey = new RsaSecurityKey(otherRsa.ExportParameters(includePrivateParameters: false));

        var tokenHandler = new JsonWebTokenHandler();
        var result = await tokenHandler.ValidateTokenAsync(_capturedFormData!["assertion"], new TokenValidationParameters
        {
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = false,
            IssuerSigningKey = otherPublicKey,
        });

        Assert.False(result.IsValid);
    }
}
