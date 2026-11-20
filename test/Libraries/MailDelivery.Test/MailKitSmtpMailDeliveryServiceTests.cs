using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Bit.Core.Models.Mail;
using Bit.Core.Platform.Mail.Delivery;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using Rnwood.SmtpServer;
using Rnwood.SmtpServer.Extensions.Auth;

namespace Bit.MailDelivery.Test;

public class MailKitSmtpMailDeliveryServiceTests
{
    private readonly MailDeliveryOptions _options;
    private readonly ILogger<MailKitSmtpMailDeliveryService> _logger;

    public MailKitSmtpMailDeliveryServiceTests()
    {
        _options = new MailDeliveryOptions();
        _logger = Substitute.For<ILogger<MailKitSmtpMailDeliveryService>>();

        _options.Smtp.Host = "unittests.example.com";
        _options.ReplyToEmail = "noreply@unittests.example.com";
    }

    private MailKitSmtpMailDeliveryService CreateSut(bool trustServer = false)
    {
        _options.Smtp.TrustServer = trustServer;
        return new MailKitSmtpMailDeliveryService(
            Options.Create(_options), _logger, Substitute.For<ISmtpCredentialProvider>());
    }

    [Fact]
    public void ValidateServerCertificate_NoPolicyErrors_ReturnsTrue()
    {
        var sut = CreateSut();
        var result = sut.ValidateServerCertificate(null!, null, null, SslPolicyErrors.None);
        Assert.True(result);
    }

    [Fact]
    public void ValidateServerCertificate_TrustServer_AnyError_ReturnsTrue()
    {
        var sut = CreateSut(trustServer: true);
        var result = sut.ValidateServerCertificate(null!, null, null, SslPolicyErrors.RemoteCertificateNameMismatch);
        Assert.True(result);
    }

    [Fact]
    public void ValidateServerCertificate_TrustServer_ChainErrors_ReturnsTrue()
    {
        var sut = CreateSut(trustServer: true);
        var result = sut.ValidateServerCertificate(null!, null, null, SslPolicyErrors.RemoteCertificateChainErrors);
        Assert.True(result);
    }

    [Fact]
    public void ValidateServerCertificate_NameMismatch_ReturnsFalse()
    {
        var sut = CreateSut();
        var result = sut.ValidateServerCertificate(null!, null, null, SslPolicyErrors.RemoteCertificateNameMismatch);
        Assert.False(result);
    }

    [Fact]
    public void ValidateServerCertificate_ChainErrorsWithNullChain_ReturnsFalse()
    {
        var sut = CreateSut();
        var result = sut.ValidateServerCertificate(null!, null, null, SslPolicyErrors.RemoteCertificateChainErrors);
        Assert.False(result);
    }

    [Fact]
    public void ValidateServerCertificate_ChainErrors_OnlyCrlStatuses_ReturnsTrue_LogsWarning()
    {
        var sut = CreateSut();
        using var chain = new X509Chain();
        // An unbuilt chain has an empty ChainStatus; All() on empty is vacuously true,
        // exercising the CRL-only branch.

        var result = sut.ValidateServerCertificate(null!, null, chain, SslPolicyErrors.RemoteCertificateChainErrors);

        Assert.True(result);
        _logger.Received(1).Log(
            LogLevel.Warning,
            Arg.Any<EventId>(),
            Arg.Any<object>(),
            null,
            Arg.Any<Func<object, Exception, string>>());
    }

    // SendEmailAsync creates its SmtpClient inline with no seam to substitute it, so these tests
    // authenticate against a real Rnwood.SmtpServer instance to exercise the retry-once path.

    private static int RandomPort() => Random.Shared.Next(50000, 60000);

    private static X509Certificate2 CreateSelfSignedCert(string commonName)
    {
        using var rsa = RSA.Create(2048);
        var certRequest = new CertificateRequest($"CN={commonName}", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        return certRequest.CreateSelfSigned(DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddYears(1));
    }

    private MailKitSmtpMailDeliveryService CreateSutForServer(
        int port, SmtpAuthType authType, ISmtpCredentialProvider credentialProvider)
    {
        _options.Smtp.Host = "localhost";
        _options.Smtp.Port = port;
        _options.Smtp.Ssl = true;
        _options.Smtp.TrustServer = true;
        _options.Smtp.AuthType = authType;
        return new MailKitSmtpMailDeliveryService(Options.Create(_options), _logger, credentialProvider);
    }

    private static Task SendTestMessageAsync(MailKitSmtpMailDeliveryService sut, CancellationToken cancellationToken)
        => sut.SendEmailAsync(new MailMessage
        {
            Subject = "Test",
            ToEmails = ["test1@example.com"],
            TextContent = "Hi",
        }, cancellationToken);

    [Fact]
    public async Task SendEmailAsync_Password_NeverCallsCredentialProvider()
    {
        var port = RandomPort();
        var behavior = new DefaultServerBehaviour(false, port, CreateSelfSignedCert("localhost"));
        behavior.AuthenticationCredentialsValidationRequiredEventHandler += (sender, args) =>
        {
            args.AuthenticationResult =
                args.Credentials is UsernameAndPasswordAuthenticationCredentials { Username: "test", Password: "password" }
                    ? AuthenticationResult.Success
                    : AuthenticationResult.Failure;
            return Task.CompletedTask;
        };
        using var smtpServer = new SmtpServer(behavior);
        smtpServer.Start();

        _options.Smtp.Username = "test";
        _options.Smtp.Password = "password";
        var credentialProvider = Substitute.For<ISmtpCredentialProvider>();
        var sut = CreateSutForServer(port, SmtpAuthType.Password, credentialProvider);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await SendTestMessageAsync(sut, cts.Token);

        await credentialProvider.DidNotReceive().GetCredentialsAsync(Arg.Any<CancellationToken>());
        await credentialProvider.DidNotReceive().InvalidateAsync(Arg.Any<SaslMechanism>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SendEmailAsync_OAuth_Success_CallsGetCredentialsOnceAndNeverInvalidates()
    {
        var port = RandomPort();
        var behavior = new DefaultServerBehaviour(false, port, CreateSelfSignedCert("localhost"));
        behavior.AuthenticationCredentialsValidationRequiredEventHandler += (sender, args) =>
        {
            args.AuthenticationResult =
                args.Credentials is UsernameAndPasswordAuthenticationCredentials { Password: "good-token" }
                    ? AuthenticationResult.Success
                    : AuthenticationResult.Failure;
            return Task.CompletedTask;
        };
        using var smtpServer = new SmtpServer(behavior);
        smtpServer.Start();

        var credentialProvider = Substitute.For<ISmtpCredentialProvider>();
        credentialProvider.GetCredentialsAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<SaslMechanism>(new SaslMechanismPlain("oauth-user", "good-token")));

        var sut = CreateSutForServer(port, SmtpAuthType.MicrosoftOAuth, credentialProvider);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await SendTestMessageAsync(sut, cts.Token);

        await credentialProvider.Received(1).GetCredentialsAsync(Arg.Any<CancellationToken>());
        await credentialProvider.DidNotReceive().InvalidateAsync(Arg.Any<SaslMechanism>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SendEmailAsync_OAuth_FirstAuthenticationRejected_InvalidatesAndRetriesOnce()
    {
        var port = RandomPort();
        var behavior = new DefaultServerBehaviour(false, port, CreateSelfSignedCert("localhost"));
        behavior.AuthenticationCredentialsValidationRequiredEventHandler += (sender, args) =>
        {
            args.AuthenticationResult =
                args.Credentials is UsernameAndPasswordAuthenticationCredentials { Password: "good-token" }
                    ? AuthenticationResult.Success
                    : AuthenticationResult.Failure;
            return Task.CompletedTask;
        };
        using var smtpServer = new SmtpServer(behavior);
        smtpServer.Start();

        var rejectedCredentials = new SaslMechanismPlain("oauth-user", "bad-token");
        var acceptedCredentials = new SaslMechanismPlain("oauth-user", "good-token");
        var credentialProvider = Substitute.For<ISmtpCredentialProvider>();
        credentialProvider.GetCredentialsAsync(Arg.Any<CancellationToken>())
            .Returns(
                Task.FromResult<SaslMechanism>(rejectedCredentials),
                Task.FromResult<SaslMechanism>(acceptedCredentials));

        var sut = CreateSutForServer(port, SmtpAuthType.MicrosoftOAuth, credentialProvider);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await SendTestMessageAsync(sut, cts.Token);

        await credentialProvider.Received(1).InvalidateAsync(rejectedCredentials, Arg.Any<CancellationToken>());
        await credentialProvider.Received(2).GetCredentialsAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SendEmailAsync_OAuth_SecondAuthenticationRejected_PropagatesAndDoesNotRetryAgain()
    {
        var port = RandomPort();
        var behavior = new DefaultServerBehaviour(false, port, CreateSelfSignedCert("localhost"));
        behavior.AuthenticationCredentialsValidationRequiredEventHandler += (sender, args) =>
        {
            args.AuthenticationResult = AuthenticationResult.Failure;
            return Task.CompletedTask;
        };
        using var smtpServer = new SmtpServer(behavior);
        smtpServer.Start();

        var credentialProvider = Substitute.For<ISmtpCredentialProvider>();
        credentialProvider.GetCredentialsAsync(Arg.Any<CancellationToken>())
            .Returns(
                Task.FromResult<SaslMechanism>(new SaslMechanismPlain("oauth-user", "bad-token-1")),
                Task.FromResult<SaslMechanism>(new SaslMechanismPlain("oauth-user", "bad-token-2")));

        var sut = CreateSutForServer(port, SmtpAuthType.MicrosoftOAuth, credentialProvider);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await Assert.ThrowsAsync<AuthenticationException>(() => SendTestMessageAsync(sut, cts.Token));

        await credentialProvider.Received(2).GetCredentialsAsync(Arg.Any<CancellationToken>());
    }
}
