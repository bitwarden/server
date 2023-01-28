using Bit.Core.Models.Mail;
using Bit.Core.Services;
using Bit.Core.Settings;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using NSubstitute;
using SendGrid;
using SendGrid.Helpers.Mail;
using Xunit;

namespace Bit.Core.Test.Services;

public class SendGridMailDeliveryServiceTests : IDisposable
{
    private readonly SendGridMailDeliveryService _sut;

    private readonly GlobalSettings _globalSettings;
    private readonly IWebHostEnvironment _hostingEnvironment;
    private readonly ILogger<SendGridMailDeliveryService> _logger;
    private readonly ISendGridClient _sendGridClient;

    public SendGridMailDeliveryServiceTests()
    {
        _globalSettings = new GlobalSettings
        {
            Mail =
            {
                SendGridApiKey = "SendGridApiKey"
            }
        };

        _hostingEnvironment = Substitute.For<IWebHostEnvironment>();
        _logger = Substitute.For<ILogger<SendGridMailDeliveryService>>();
        _sendGridClient = Substitute.For<ISendGridClient>();

        _sut = new SendGridMailDeliveryService(
            _sendGridClient,
            _globalSettings,
            _hostingEnvironment,
            _logger
        );
    }

    public void Dispose()
    {
        _sut?.Dispose();
    }

    [Fact]
    public async Task SendEmailAsync_CallsSendEmailAsync_WhenMessageIsValid()
    {
        var mailMessage = new MailMessage
        {
            ToEmails = new List<string> { "ToEmails" },
            BccEmails = new List<string> { "BccEmails" },
            Subject = "Subject",
            HtmlContent = "HtmlContent",
            TextContent = "TextContent",
            Category = "Category"
        };

        _sendGridClient.SendEmailAsync(Arg.Any<SendGridMessage>()).Returns(
            new Response(System.Net.HttpStatusCode.OK, null, null));
        await _sut.SendEmailAsync(mailMessage);

        await _sendGridClient.Received(1).SendEmailAsync(
            Arg.Do<SendGridMessage>(msg =>
            {
                msg.Received(1).AddTos(new List<EmailAddress> { new EmailAddress(mailMessage.ToEmails.First()) });
                msg.Received(1).AddBccs(new List<EmailAddress> { new EmailAddress(mailMessage.ToEmails.First()) });

                Assert.Equal(mailMessage.Subject, msg.Subject);
                Assert.Equal(mailMessage.HtmlContent, msg.HtmlContent);
                Assert.Equal(mailMessage.TextContent, msg.PlainTextContent);

                Assert.Contains("type:Cateogry", msg.Categories);
                Assert.Contains(msg.Categories, x => x.StartsWith("env:"));
                Assert.Contains(msg.Categories, x => x.StartsWith("sender:"));

                msg.Received(1).SetClickTracking(false, false);
                msg.Received(1).SetOpenTracking(false);
            }));
    }
}
