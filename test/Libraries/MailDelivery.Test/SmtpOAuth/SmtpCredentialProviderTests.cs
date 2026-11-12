using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using Bit.Core.Platform.Mail.Delivery;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using RichardSzalay.MockHttp;

namespace Bit.MailDelivery.Test.SmtpOAuth;

public class SmtpCredentialProviderTests
{
    private readonly MailDeliveryOptions _options;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly FakeTimeProvider _timeProvider;
    private readonly ILoggerFactory _loggerFactory;
    private readonly MockHttpMessageHandler _mockHandler;

    public SmtpCredentialProviderTests()
    {
        _options = new MailDeliveryOptions();
        _httpClientFactory = Substitute.For<IHttpClientFactory>();
        _timeProvider = new FakeTimeProvider();
        _loggerFactory = Substitute.For<ILoggerFactory>();
        _mockHandler = new MockHttpMessageHandler();
        _httpClientFactory.CreateClient("SmtpOAuth").Returns(_mockHandler.ToHttpClient());
    }

    private SmtpCredentialProvider CreateSut()
        => new(Options.Create(_options), _httpClientFactory, _timeProvider, _loggerFactory);

    [Fact]
    public void Constructor_Password_DoesNotThrow()
    {
        _options.Smtp.AuthType = SmtpAuthType.Password;

        var sut = CreateSut();

        Assert.NotNull(sut);
    }

    [Fact]
    public async Task GetCredentialsAsync_Password_ThrowsInvalidOperationException()
    {
        _options.Smtp.AuthType = SmtpAuthType.Password;
        var sut = CreateSut();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => sut.GetCredentialsAsync(CancellationToken.None));
    }

    [Fact]
    public async Task InvalidateAsync_NonOAuth2Credentials_DoesNotThrow()
    {
        _options.Smtp.AuthType = SmtpAuthType.MicrosoftOAuth;
        _options.Smtp.OutboundMailbox = "smtp-user@example.com";
        _options.Smtp.OAuth.TokenEndpoint = "https://login.microsoftonline.com/tenant-id/oauth2/v2.0/token";
        _options.Smtp.OAuth.ClientId = "client-id";
        _options.Smtp.OAuth.ClientSecret = "client-secret";
        _options.Smtp.OAuth.Scope = "https://outlook.office365.com/.default";
        var sut = CreateSut();

        await sut.InvalidateAsync(new SaslMechanismPlain("smtp-user@example.com", "password"), CancellationToken.None);
    }

    [Fact]
    public void Constructor_MicrosoftOAuth_DoesNotThrow()
    {
        _options.Smtp.AuthType = SmtpAuthType.MicrosoftOAuth;
        _options.Smtp.OutboundMailbox = "smtp-user@example.com";
        _options.Smtp.OAuth.TokenEndpoint = "https://login.microsoftonline.com/tenant-id/oauth2/v2.0/token";
        _options.Smtp.OAuth.ClientId = "client-id";
        _options.Smtp.OAuth.ClientSecret = "client-secret";
        _options.Smtp.OAuth.Scope = "https://outlook.office365.com/.default";

        var sut = CreateSut();

        Assert.NotNull(sut);
    }

    [Fact]
    public void Constructor_CustomOAuth_DoesNotThrow()
    {
        _options.Smtp.AuthType = SmtpAuthType.CustomOAuth;
        _options.Smtp.OutboundMailbox = "smtp-user@example.com";
        _options.Smtp.OAuth.TokenEndpoint = "https://auth.example.com/oauth2/token";
        _options.Smtp.OAuth.ClientId = "client-id";
        _options.Smtp.OAuth.ClientSecret = "client-secret";
        _options.Smtp.OAuth.Scope = "smtp.send";

        var sut = CreateSut();

        Assert.NotNull(sut);
    }

    [Fact]
    public void Constructor_CustomOAuth_JwtBearer_DoesNotThrow()
    {
        using var rsa = RSA.Create(2048);
        _options.Smtp.AuthType = SmtpAuthType.CustomOAuth;
        _options.Smtp.OutboundMailbox = "smtp-user@example.com";
        _options.Smtp.OAuth.TokenEndpoint = "https://auth.example.com/oauth2/token";
        _options.Smtp.OAuth.Scope = "smtp.send";
        _options.Smtp.OAuth.GrantType = "urn:ietf:params:oauth:grant-type:jwt-bearer";
        _options.Smtp.OAuth.SigningKey = rsa.ExportPkcs8PrivateKeyPem();
        _options.Smtp.OAuth.Claims["iss"] = "service-account@example.com";

        var sut = CreateSut();

        Assert.NotNull(sut);
    }

    [Fact]
    public async Task GetCredentialsAsync_CustomOAuth_JwtBearer_FetchesToken()
    {
        using var rsa = RSA.Create(2048);
        _options.Smtp.AuthType = SmtpAuthType.CustomOAuth;
        _options.Smtp.OutboundMailbox = "smtp-user@example.com";
        _options.Smtp.OAuth.TokenEndpoint = "https://auth.example.com/oauth2/token";
        _options.Smtp.OAuth.Scope = "smtp.send";
        _options.Smtp.OAuth.GrantType = "urn:ietf:params:oauth:grant-type:jwt-bearer";
        _options.Smtp.OAuth.SigningKey = rsa.ExportPkcs8PrivateKeyPem();
        _options.Smtp.OAuth.Claims["iss"] = "service-account@example.com";

        _mockHandler
            .Expect(HttpMethod.Post, "https://auth.example.com/oauth2/token")
            .Respond(_ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new { access_token = "jwt-bearer-token", expires_in = 3599 }),
            });

        var sut = CreateSut();

        var credentials = Assert.IsType<SaslMechanismOAuth2>(
            await sut.GetCredentialsAsync(CancellationToken.None));
        Assert.Equal("jwt-bearer-token", credentials.Credentials.Password);

        _mockHandler.VerifyNoOutstandingExpectation();
    }

    [Fact]
    public void Constructor_GoogleOAuth_DoesNotThrow()
    {
        using var rsa = RSA.Create(2048);
        _options.Smtp.AuthType = SmtpAuthType.GoogleOAuth;
        _options.Smtp.OutboundMailbox = "smtp-user@example.com";
        _options.Smtp.OAuth.TokenEndpoint = "https://oauth2.googleapis.com/token";
        _options.Smtp.OAuth.GrantType = "urn:ietf:params:oauth:grant-type:jwt-bearer";
        _options.Smtp.OAuth.SigningKey = rsa.ExportPkcs8PrivateKeyPem();
        _options.Smtp.OAuth.Claims["iss"] = "service-account@example.com";

        var sut = CreateSut();

        Assert.NotNull(sut);
    }

    [Fact]
    public async Task InvalidateAsync_OAuth2Credentials_NextGetCredentialsFetchesNewToken()
    {
        _options.Smtp.AuthType = SmtpAuthType.MicrosoftOAuth;
        _options.Smtp.OutboundMailbox = "smtp-user@example.com";
        _options.Smtp.OAuth.TokenEndpoint = "https://login.microsoftonline.com/tenant-id/oauth2/v2.0/token";
        _options.Smtp.OAuth.ClientId = "client-id";
        _options.Smtp.OAuth.ClientSecret = "client-secret";
        _options.Smtp.OAuth.Scope = "https://outlook.office365.com/.default";

        _mockHandler
            .Expect(HttpMethod.Post, "https://login.microsoftonline.com/tenant-id/oauth2/v2.0/token")
            .Respond(_ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new { access_token = "token_a", expires_in = 3599 }),
            });
        _mockHandler
            .Expect(HttpMethod.Post, "https://login.microsoftonline.com/tenant-id/oauth2/v2.0/token")
            .Respond(_ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new { access_token = "token_b", expires_in = 3599 }),
            });

        var sut = CreateSut();

        var firstCredentials = Assert.IsType<SaslMechanismOAuth2>(
            await sut.GetCredentialsAsync(CancellationToken.None));
        Assert.Equal("token_a", firstCredentials.Credentials.Password);

        await sut.InvalidateAsync(firstCredentials, CancellationToken.None);

        var secondCredentials = Assert.IsType<SaslMechanismOAuth2>(
            await sut.GetCredentialsAsync(CancellationToken.None));
        Assert.Equal("token_b", secondCredentials.Credentials.Password);

        _mockHandler.VerifyNoOutstandingExpectation();
    }
}
