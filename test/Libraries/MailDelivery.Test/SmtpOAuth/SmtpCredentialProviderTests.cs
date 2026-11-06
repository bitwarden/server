using Bit.Core.Platform.Mail.Delivery;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;

namespace Bit.MailDelivery.Test.SmtpOAuth;

public class SmtpCredentialProviderTests
{
    private readonly MailDeliveryOptions _options;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly FakeTimeProvider _timeProvider;
    private readonly ILoggerFactory _loggerFactory;

    public SmtpCredentialProviderTests()
    {
        _options = new MailDeliveryOptions();
        _httpClientFactory = Substitute.For<IHttpClientFactory>();
        _timeProvider = new FakeTimeProvider();
        _loggerFactory = Substitute.For<ILoggerFactory>();
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
        _options.Smtp.Username = "smtp-user@example.com";
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
        _options.Smtp.Username = "smtp-user@example.com";
        _options.Smtp.OAuth.TokenEndpoint = "https://login.microsoftonline.com/tenant-id/oauth2/v2.0/token";
        _options.Smtp.OAuth.ClientId = "client-id";
        _options.Smtp.OAuth.ClientSecret = "client-secret";
        _options.Smtp.OAuth.Scope = "https://outlook.office365.com/.default";

        var sut = CreateSut();

        Assert.NotNull(sut);
    }

    [Fact]
    public void Constructor_CustomOAuth_ThrowsNotSupportedException()
    {
        _options.Smtp.AuthType = SmtpAuthType.CustomOAuth;

        Assert.Throws<NotSupportedException>(() => CreateSut());
    }
}
