using Bit.Core.Services;
using Bit.Core.Settings;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.Services;

public class MailKitSmtpMailDeliveryServiceTests
{
    private readonly MailKitSmtpMailDeliveryService _sut;

    private readonly GlobalSettings _globalSettings;
    private readonly ILogger<MailKitSmtpMailDeliveryService> _logger;

    public MailKitSmtpMailDeliveryServiceTests()
    {
        _globalSettings = new GlobalSettings();
        _logger = Substitute.For<ILogger<MailKitSmtpMailDeliveryService>>();

        _globalSettings.Mail.Smtp.Host = "unittests.example.com";
        _globalSettings.Mail.ReplyToEmail = "noreply@unittests.example.com";

        _sut = new MailKitSmtpMailDeliveryService(_globalSettings, _logger);
    }

    // Remove this test when we add actual tests. It only proves that
    // we've properly constructed the system under test.
    [Fact]
    public void ServiceExists()
    {
        Assert.NotNull(_sut);
    }
}
