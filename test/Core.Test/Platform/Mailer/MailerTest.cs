using Bit.Core.Models.Mail;
using Bit.Core.Platform.Mail.Delivery;
using Bit.Core.Platform.Mail.Enqueuing;
using Bit.Core.Platform.Mail.Mailer;
using Bit.Core.Settings;
using Bit.Core.Test.Platform.Mailer.TestMail;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.Platform.Mailer;

public class MailerTest
{
    private readonly IMailRenderer _mockRenderer;
    private readonly IMailDeliveryService _mockDeliveryService;
    private readonly IMailEnqueuingService _mockEnqueuingService;
    private readonly Core.Platform.Mail.Mailer.Mailer _mailer;

    public MailerTest()
    {
        _mockRenderer = Substitute.For<IMailRenderer>();
        _mockDeliveryService = Substitute.For<IMailDeliveryService>();
        _mockEnqueuingService = Substitute.For<IMailEnqueuingService>();
        _mailer = new Core.Platform.Mail.Mailer.Mailer(_mockRenderer, _mockDeliveryService, _mockEnqueuingService);
    }

    [Fact]
    public async Task SendEmailAsync()
    {
        var logger = Substitute.For<ILogger<HandlebarMailRenderer>>();
        var globalSettings = new GlobalSettings { SelfHosted = false };
        var deliveryService = Substitute.For<IMailDeliveryService>();
        var enqueuingService = Substitute.For<IMailEnqueuingService>();

        var mailer = new Core.Platform.Mail.Mailer.Mailer(new HandlebarMailRenderer(logger, globalSettings), deliveryService, enqueuingService);

        var mail = new TestMail.TestMail()
        {
            ToEmails = ["test@bw.com"],
            View = new TestMailView() { Name = "John Smith" }
        };

        MailMessage? sentMessage = null;
        await deliveryService.SendEmailAsync(Arg.Do<MailMessage>(message =>
            sentMessage = message
        ));

        await mailer.SendEmail(mail);

        Assert.NotNull(sentMessage);
        Assert.Contains("test@bw.com", sentMessage.ToEmails);
        Assert.Equal("Test Email", sentMessage.Subject);
        Assert.Equivalent("Hello John Smith", sentMessage.TextContent.Trim());
        Assert.Equivalent("Hello <b>John Smith</b>", sentMessage.HtmlContent.Trim());
    }

    [Fact]
    public async Task EnqueueEmailsAsync_StoresViewDataAndDoesNotRender()
    {
        var testView = new TestMailView { Name = "Test Content" };
        var testMail = new TestMail.TestMail
        {
            ToEmails = ["test@example.com"],
            View = testView
        };

        List<MailQueueMessage> capturedMessages = null;
        _mockEnqueuingService.EnqueueManyAsync(
            Arg.Do<IEnumerable<IMailQueueMessage>>(msgs =>
                capturedMessages = msgs.Cast<MailQueueMessage>().ToList()),
            Arg.Any<Func<IMailQueueMessage, Task>>())
            .Returns(Task.CompletedTask);

        await _mailer.EnqueueEmailsAsync([testMail]);

        await _mockRenderer.DidNotReceive().RenderAsync(Arg.Any<BaseMailView>());
        Assert.NotNull(capturedMessages);
        Assert.Single(capturedMessages);

        var message = capturedMessages[0];
        Assert.Equal("Test Email", message.Subject);
        Assert.Equal(["test@example.com"], message.ToEmails);
        Assert.Equal("Default", message.Category);
        Assert.Equal(typeof(TestMailView).AssemblyQualifiedName, message.TemplateName);
        Assert.IsType<TestMailView>(message.Model);
        Assert.True(message.IsMailerMessage);
    }

    [Fact]
    public async Task EnqueueEmailsAsync_HandlesMetadataCorrectly()
    {
        var messages = new BaseMail<TestMailView>[]
        {
            new TestMailIgnoreSuppressList
            {
                ToEmails = ["test1@example.com"],
                View = new TestMailView { Name = "Content 1" }
            },
            new TestMail.TestMail
            {
                ToEmails = ["test2@example.com"],
                View = new TestMailView { Name = "Content 2" }
            }
        };

        List<MailQueueMessage> capturedMessages = null;
        _mockEnqueuingService.EnqueueManyAsync(
            Arg.Do<IEnumerable<IMailQueueMessage>>(msgs =>
                capturedMessages = msgs.Cast<MailQueueMessage>().ToList()),
            Arg.Any<Func<IMailQueueMessage, Task>>())
            .Returns(Task.CompletedTask);

        await _mailer.EnqueueEmailsAsync(messages);

        Assert.NotNull(capturedMessages);
        Assert.Equal(2, capturedMessages.Count);

        Assert.True(capturedMessages[0].MetaData!.ContainsKey("SendGridBypassListManagement"));
        Assert.True((bool)capturedMessages[0].MetaData["SendGridBypassListManagement"]);

        Assert.False(capturedMessages[1].MetaData!.ContainsKey("SendGridBypassListManagement"));
    }

    [Fact]
    public async Task EnqueueEmailsAsync_ProcessesMultipleMessages()
    {
        var messages = new[]
        {
            new TestMail.TestMail
            {
                ToEmails = ["test1@example.com"],
                Subject = "Subject 1",
                View = new TestMailView { Name = "Content 1" }
            },
            new TestMail.TestMail
            {
                ToEmails = ["test2@example.com"],
                Subject = "Subject 2",
                View = new TestMailView { Name = "Content 2" }
            },
            new TestMail.TestMail
            {
                ToEmails = ["test3@example.com"],
                Subject = "Subject 3",
                View = new TestMailView { Name = "Content 3" }
            }
        };

        List<MailQueueMessage> capturedMessages = null;
        _mockEnqueuingService.EnqueueManyAsync(
            Arg.Do<IEnumerable<IMailQueueMessage>>(msgs =>
                capturedMessages = msgs.Cast<MailQueueMessage>().ToList()),
            Arg.Any<Func<IMailQueueMessage, Task>>())
            .Returns(Task.CompletedTask);

        await _mailer.EnqueueEmailsAsync(messages);

        await _mockRenderer.DidNotReceive().RenderAsync(Arg.Any<BaseMailView>());
        Assert.NotNull(capturedMessages);
        Assert.Equal(3, capturedMessages.Count);
        Assert.All(capturedMessages, msg => Assert.True(msg.IsMailerMessage));
    }

    [Fact]
    public async Task SendEnqueuedMailerMessageAsync_RendersAndSends()
    {
        var testView = new TestMailView { Name = "Test Content" };
        var queueMessage = new MailQueueMessage
        {
            Subject = "Test Subject",
            ToEmails = ["test@example.com"],
            Category = "TestCategory",
            TemplateName = typeof(TestMailView).AssemblyQualifiedName!,
            Model = testView,
            IsMailerMessage = true,
            MetaData = new Dictionary<string, object> { { "SendGridBypassListManagement", true } }
        };

        _mockRenderer.RenderAsync(Arg.Any<BaseMailView>())
            .Returns(("<html>Test</html>", "Test"));

        MailMessage? sentMessage = null;
        await _mockDeliveryService.SendEmailAsync(Arg.Do<MailMessage>(msg => sentMessage = msg));

        await _mailer.SendEnqueuedMailerMessageAsync(queueMessage);

        await _mockRenderer.Received(1).RenderAsync(Arg.Is<TestMailView>(v => v == testView));
        await _mockDeliveryService.Received(1).SendEmailAsync(Arg.Any<MailMessage>());

        Assert.NotNull(sentMessage);
        Assert.Equal("Test Subject", sentMessage.Subject);
        Assert.Equal(["test@example.com"], sentMessage.ToEmails);
        Assert.Equal("TestCategory", sentMessage.Category);
        Assert.Equal("<html>Test</html>", sentMessage.HtmlContent);
        Assert.Equal("Test", sentMessage.TextContent);
        Assert.True(sentMessage.MetaData.ContainsKey("SendGridBypassListManagement"));
        Assert.True((bool)sentMessage.MetaData["SendGridBypassListManagement"]);
    }

    [Fact]
    public async Task SendEnqueuedMailerMessageAsync_ThrowsWhenNotMailerMessage()
    {
        var queueMessage = new MailQueueMessage
        {
            Subject = "Test",
            ToEmails = ["test@example.com"],
            IsMailerMessage = false
        };

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _mailer.SendEnqueuedMailerMessageAsync(queueMessage));
    }

    [Fact]
    public async Task EnqueueAndSend_FullRoundTrip()
    {
        var testView = new TestMailView { Name = "John Doe" };
        var testMail = new TestMailIgnoreSuppressList
        {
            ToEmails = ["test@example.com"],
            Subject = "Round Trip Test",
            View = testView
        };

        MailQueueMessage capturedQueueMessage = null;
        _mockEnqueuingService.EnqueueManyAsync(
            Arg.Do<IEnumerable<IMailQueueMessage>>(msgs =>
                capturedQueueMessage = msgs.Cast<MailQueueMessage>().First()),
            Arg.Any<Func<IMailQueueMessage, Task>>())
            .Returns(Task.CompletedTask);

        _mockRenderer.RenderAsync(Arg.Any<BaseMailView>())
            .Returns(("<html>Hello John Doe</html>", "Hello John Doe"));

        MailMessage sentMessage = null;
        await _mockDeliveryService.SendEmailAsync(Arg.Do<MailMessage>(msg => sentMessage = msg));

        // Act
        await _mailer.EnqueueEmailsAsync([testMail]);
        await _mailer.SendEnqueuedMailerMessageAsync(capturedQueueMessage);

        // Assert
        await _mockRenderer.Received(1).RenderAsync(Arg.Is<TestMailView>(v => v.Name == "John Doe"));
        await _mockDeliveryService.Received(1).SendEmailAsync(Arg.Any<MailMessage>());

        Assert.NotNull(sentMessage);
        Assert.Equal("Round Trip Test", sentMessage.Subject);
        Assert.Equal(["test@example.com"], sentMessage.ToEmails);
        Assert.Equal("Default", sentMessage.Category);
        Assert.Equal("<html>Hello John Doe</html>", sentMessage.HtmlContent);
        Assert.Equal("Hello John Doe", sentMessage.TextContent);
        Assert.True(sentMessage.MetaData.ContainsKey("SendGridBypassListManagement"));
        Assert.True((bool)sentMessage.MetaData["SendGridBypassListManagement"]);
    }
}

// Test helper class with IgnoreSuppressList override
public class TestMailIgnoreSuppressList : BaseMail<TestMailView>
{
    public override string Subject { get; set; } = "Test Subject";
    public override bool IgnoreSuppressList => true;
}
