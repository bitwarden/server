using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using Bit.Core.Platform.Mail.Delivery;
using Bit.Core.Settings;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.Services;

public class MailKitSmtpMailDeliveryServiceTests
{
    private readonly GlobalSettings _globalSettings;
    private readonly ILogger<MailKitSmtpMailDeliveryService> _logger;

    public MailKitSmtpMailDeliveryServiceTests()
    {
        _globalSettings = new GlobalSettings();
        _logger = Substitute.For<ILogger<MailKitSmtpMailDeliveryService>>();

        _globalSettings.Mail.Smtp.Host = "unittests.example.com";
        _globalSettings.Mail.ReplyToEmail = "noreply@unittests.example.com";
    }

    private MailKitSmtpMailDeliveryService CreateSut(bool trustServer = false)
    {
        _globalSettings.Mail.Smtp.TrustServer = trustServer;
        return new MailKitSmtpMailDeliveryService(_globalSettings, _logger);
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
}
