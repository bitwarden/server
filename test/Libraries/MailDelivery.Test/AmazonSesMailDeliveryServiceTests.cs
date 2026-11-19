using Amazon.SimpleEmail;
using Amazon.SimpleEmail.Model;
using Bit.Core.Models.Mail;
using Bit.Core.Platform.Mail.Delivery;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Bit.MailDelivery.Test;

public class AmazonSesMailDeliveryServiceTests : IDisposable
{
    private readonly AmazonSesMailDeliveryService _sut;

    private readonly MailDeliveryOptions _options;
    private readonly IHostEnvironment _hostingEnvironment;
    private readonly ILogger<AmazonSesMailDeliveryService> _logger;
    private readonly IAmazonSimpleEmailService _amazonSimpleEmailService;

    public AmazonSesMailDeliveryServiceTests()
    {
        _options = new MailDeliveryOptions
        {
            Amazon =
                    {
                        AccessKeyId = "AccessKeyId-AmazonSesMailDeliveryServiceTests",
                        AccessKeySecret = "AccessKeySecret-AmazonSesMailDeliveryServiceTests",
                        Region = "Region-AmazonSesMailDeliveryServiceTests"
                    },
            ReplyToEmail = "no-reply@bitwarden.com"
        };

        _hostingEnvironment = Substitute.For<IHostEnvironment>();
        _logger = Substitute.For<ILogger<AmazonSesMailDeliveryService>>();
        _amazonSimpleEmailService = Substitute.For<IAmazonSimpleEmailService>();

        _sut = new AmazonSesMailDeliveryService(
            Options.Create(_options),
            _hostingEnvironment,
            _logger,
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

    [Fact]
    public async Task SendEmailAsync_WithReplyToAddress_SetsReplyToAddresses()
    {
        var captured = await CaptureRequestAsync("support@bitwarden.com");

        Assert.Equal(["support@bitwarden.com"], captured.ReplyToAddresses);
        Assert.Contains("no-reply@bitwarden.com", captured.Source);
        Assert.DoesNotContain("support@bitwarden.com", captured.Source);
    }

    [Fact]
    public async Task SendEmailAsync_WithoutReplyToAddress_LeavesReplyToAddressesUnset()
    {
        var captured = await CaptureRequestAsync(replyToAddress: null);

        Assert.Null(captured.ReplyToAddresses);
    }

    /// <remarks>
    /// Captures at call time and asserts outside the callback: an <c>Arg.Do</c> passed to
    /// <c>Received()</c> never runs, so assertions placed inside one do not execute.
    /// </remarks>
    private async Task<SendEmailRequest> CaptureRequestAsync(string? replyToAddress)
    {
        SendEmailRequest? captured = null;
        await _amazonSimpleEmailService.SendEmailAsync(Arg.Do<SendEmailRequest>(r => captured = r));

        await _sut.SendEmailAsync(new MailMessage
        {
            ToEmails = new List<string> { "to@example.com" },
            Subject = "Subject",
            HtmlContent = "HtmlContent",
            TextContent = "TextContent",
            ReplyToAddress = replyToAddress
        });

        Assert.NotNull(captured);
        return captured;
    }
}
