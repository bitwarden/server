using Amazon.SimpleEmail;
using Amazon.SimpleEmail.Model;
using Bit.Core.Models.Mail;
using Bit.Core.Platform.MailDelivery;
using Bit.Core.Settings;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.Services;

public class AmazonSesMailDeliveryServiceTests : IDisposable
{
    private readonly AmazonSesMailDeliveryService _sut;

    private readonly GlobalSettings _globalSettings;
    private readonly IHostEnvironment _hostingEnvironment;
    private readonly IAmazonSimpleEmailService _amazonSimpleEmailService;

    public AmazonSesMailDeliveryServiceTests()
    {
        _globalSettings = new GlobalSettings
        {
            Amazon =
                    {
                        AccessKeyId = "AccessKeyId-AmazonSesMailDeliveryServiceTests",
                        AccessKeySecret = "AccessKeySecret-AmazonSesMailDeliveryServiceTests",
                        Region = "Region-AmazonSesMailDeliveryServiceTests"
                    }
        };

        _hostingEnvironment = Substitute.For<IHostEnvironment>();
        _amazonSimpleEmailService = Substitute.For<IAmazonSimpleEmailService>();

        _sut = new AmazonSesMailDeliveryService(
            _globalSettings,
            _hostingEnvironment,
            NullLogger<AmazonSesMailDeliveryService>.Instance,
            _amazonSimpleEmailService
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

        await _sut.SendEmailAsync(mailMessage);

        await _amazonSimpleEmailService.Received(1).SendEmailAsync(
            Arg.Do<SendEmailRequest>(request =>
            {
                Assert.False(string.IsNullOrEmpty(request.Source));

                Assert.Single(request.Destination.ToAddresses);
                Assert.Equal(mailMessage.ToEmails.First(), request.Destination.ToAddresses.First());

                Assert.Equal(mailMessage.Subject, request.Message.Subject.Data);
                Assert.Equal(mailMessage.HtmlContent, request.Message.Body.Html.Data);
                Assert.Equal(mailMessage.TextContent, request.Message.Body.Text.Data);

                Assert.Single(request.Destination.BccAddresses);
                Assert.Equal(mailMessage.BccEmails.First(), request.Destination.BccAddresses.First());

                Assert.Contains(request.Tags, x => x.Name == "Environment");
                Assert.Contains(request.Tags, x => x.Name == "Sender");
                Assert.Contains(request.Tags, x => x.Name == "Category");
            }));
    }
}
