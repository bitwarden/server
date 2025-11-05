using System.Reflection;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Entities.Provider;
using Bit.Core.Auth.Entities;
using Bit.Core.Auth.Enums;
using Bit.Core.Auth.Models.Business;
using Bit.Core.Entities;
using Bit.Core.Models.Mail;
using Bit.Core.Platform.Mail.Delivery;
using Bit.Core.Platform.Mail.Enqueuing;
using Bit.Core.Services;
using Bit.Core.Services.Mail;
using Bit.Core.Settings;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.Services;

public class HandlebarsMailServiceTests
{
    private readonly HandlebarsMailService _sut;

    private readonly GlobalSettings _globalSettings;
    private readonly IMailDeliveryService _mailDeliveryService;
    private readonly IMailEnqueuingService _mailEnqueuingService;
    private readonly IDistributedCache _distributedCache;
    private readonly ILogger<HandlebarsMailService> _logger;

    public HandlebarsMailServiceTests()
    {
        _globalSettings = new GlobalSettings();
        _mailDeliveryService = Substitute.For<IMailDeliveryService>();
        _mailEnqueuingService = Substitute.For<IMailEnqueuingService>();
        _distributedCache = Substitute.For<IDistributedCache>();
        _logger = Substitute.For<ILogger<HandlebarsMailService>>();

        _sut = new HandlebarsMailService(
            _globalSettings,
            _mailDeliveryService,
            _mailEnqueuingService,
            _distributedCache,
            _logger
        );
    }

    [Fact]
    public async Task SendFailedTwoFactorAttemptEmailAsync_FirstCall_SendsEmail()
    {
        // Arrange
        var email = "test@example.com";
        var failedType = TwoFactorProviderType.Email;
        var utcNow = DateTime.UtcNow;
        var ip = "192.168.1.1";

        _distributedCache.GetAsync(Arg.Any<string>()).Returns((byte[])null);

        // Act
        await _sut.SendFailedTwoFactorAttemptEmailAsync(email, failedType, utcNow, ip);

        // Assert
        await _mailDeliveryService.Received(1).SendEmailAsync(Arg.Any<MailMessage>());
        await _distributedCache.Received(1).SetAsync(
            Arg.Is<string>(key => key == $"FailedTwoFactorAttemptEmail_{email}"),
            Arg.Any<byte[]>(),
            Arg.Any<DistributedCacheEntryOptions>()
        );
    }

    [Fact]
    public async Task SendFailedTwoFactorAttemptEmailAsync_SecondCallWithinHour_DoesNotSendEmail()
    {
        // Arrange
        var email = "test@example.com";
        var failedType = TwoFactorProviderType.Email;
        var utcNow = DateTime.UtcNow;
        var ip = "192.168.1.1";

        // Simulate cache hit (email was already sent)
        _distributedCache.GetAsync(Arg.Any<string>()).Returns([1]);

        // Act
        await _sut.SendFailedTwoFactorAttemptEmailAsync(email, failedType, utcNow, ip);

        // Assert
        await _mailDeliveryService.DidNotReceive().SendEmailAsync(Arg.Any<MailMessage>());
        await _distributedCache.DidNotReceive().SetAsync(Arg.Any<string>(), Arg.Any<byte[]>(), Arg.Any<DistributedCacheEntryOptions>());
    }

    [Fact]
    public async Task SendFailedTwoFactorAttemptEmailAsync_DifferentEmails_SendsBothEmails()
    {
        // Arrange
        var email1 = "test1@example.com";
        var email2 = "test2@example.com";
        var failedType = TwoFactorProviderType.Email;
        var utcNow = DateTime.UtcNow;
        var ip = "192.168.1.1";

        _distributedCache.GetAsync(Arg.Any<string>()).Returns((byte[])null);

        // Act
        await _sut.SendFailedTwoFactorAttemptEmailAsync(email1, failedType, utcNow, ip);
        await _sut.SendFailedTwoFactorAttemptEmailAsync(email2, failedType, utcNow, ip);

        // Assert
        await _mailDeliveryService.Received(2).SendEmailAsync(Arg.Any<MailMessage>());
        await _distributedCache.Received(1).SetAsync(
            Arg.Is<string>(key => key == $"FailedTwoFactorAttemptEmail_{email1}"),
            Arg.Any<byte[]>(),
            Arg.Any<DistributedCacheEntryOptions>()
        );
        await _distributedCache.Received(1).SetAsync(
            Arg.Is<string>(key => key == $"FailedTwoFactorAttemptEmail_{email2}"),
            Arg.Any<byte[]>(),
            Arg.Any<DistributedCacheEntryOptions>()
        );
    }

    [Fact(Skip = "For local development")]
    public async Task SendAllEmails()
    {
        // This test is only opt in and is more for development purposes.
        // This will send all emails to the test email address so that they can be viewed.
        var namedParameters = new Dictionary<(string, Type), object>
        {
            // TODO: Switch to use env variable
            { ("email", typeof(string)), "test@bitwarden.com" },
            { ("user", typeof(User)), new User
            {
                Id = Guid.NewGuid(),
                Email = "test@bitwarden.com",
            }},
            { ("userId", typeof(Guid)), Guid.NewGuid() },
            { ("token", typeof(string)), "test_token" },
            { ("fromEmail", typeof(string)), "test@bitwarden.com" },
            { ("toEmail", typeof(string)), "test@bitwarden.com" },
            { ("newEmailAddress", typeof(string)), "test@bitwarden.com" },
            { ("hint", typeof(string)), "Test Hint" },
            { ("organizationName", typeof(string)), "Test Organization Name" },
            { ("orgUser", typeof(OrganizationUser)), new OrganizationUser
            {
                Id = Guid.NewGuid(),
                Email = "test@bitwarden.com",
                OrganizationId = Guid.NewGuid(),

            }},
            { ("token", typeof(ExpiringToken)), new ExpiringToken("test_token", DateTime.UtcNow.AddDays(1))},
            { ("organization", typeof(Organization)), new Organization
            {
                Id = Guid.NewGuid(),
                Name = "Test Organization Name",
                Seats = 5
            }},
            { ("initialSeatCount", typeof(int)), 5},
            { ("ownerEmails", typeof(IEnumerable<string>)), new [] { "test@bitwarden.com" }},
            { ("maxSeatCount", typeof(int)), 5 },
            { ("userIdentifier", typeof(string)), "test_user" },
            { ("adminEmails", typeof(IEnumerable<string>)), new [] { "test@bitwarden.com" }},
            { ("returnUrl", typeof(string)), "https://bitwarden.com/" },
            { ("amount", typeof(decimal)), 1.00M },
            { ("dueDate", typeof(DateTime)), DateTime.UtcNow.AddDays(1) },
            { ("items", typeof(List<string>)), new List<string> { "test@bitwarden.com" }},
            { ("mentionInvoices", typeof(bool)), true },
            { ("emails", typeof(IEnumerable<string>)), new [] { "test@bitwarden.com" }},
            { ("deviceType", typeof(string)), "Mobile" },
            { ("timestamp", typeof(DateTime)), DateTime.UtcNow.AddDays(1)},
            { ("ip", typeof(string)), "127.0.0.1" },
            { ("emergencyAccess", typeof(EmergencyAccess)), new EmergencyAccess
            {
                Id = Guid.NewGuid(),
                Email = "test@bitwarden.com",
            }},
            { ("granteeEmail", typeof(string)), "test@bitwarden.com" },
            { ("grantorName", typeof(string)), "Test User" },
            { ("initiatingName", typeof(string)), "Test" },
            { ("approvingName", typeof(string)), "Test Name" },
            { ("rejectingName", typeof(string)), "Test Name" },
            { ("provider", typeof(Provider)), new Provider
            {
                Id = Guid.NewGuid(),
            }},
            { ("name", typeof(string)), "Test Name" },
            { ("ea", typeof(EmergencyAccess)), new EmergencyAccess
            {
                Id = Guid.NewGuid(),
                Email = "test@bitwarden.com",
            }},
            { ("userName", typeof(string)), "testUser" },
            { ("orgName", typeof(string)), "Test Org Name" },
            { ("providerName", typeof(string)), "testProvider" },
            { ("providerUser", typeof(ProviderUser)), new ProviderUser
            {
                ProviderId = Guid.NewGuid(),
                Id = Guid.NewGuid(),
            }},
            { ("familyUserEmail", typeof(string)), "test@bitwarden.com" },
            { ("sponsorEmail", typeof(string)), "test@bitwarden.com" },
            { ("familyOrgName", typeof(string)), "Test Org Name" },
            // Swap existingAccount to true or false to generate different versions of the SendFamiliesForEnterpriseOfferEmailAsync emails.
            { ("existingAccount", typeof(bool)), false },
            { ("sponsorshipEndDate", typeof(DateTime)), DateTime.UtcNow.AddDays(1)},
            { ("sponsorOrgName", typeof(string)), "Sponsor Test Org Name" },
            { ("expirationDate", typeof(DateTime)), DateTime.Now.AddDays(3) },
            { ("utcNow", typeof(DateTime)), DateTime.UtcNow },
        };

        var globalSettings = new GlobalSettings
        {
            Mail = new GlobalSettings.MailSettings
            {
                Smtp = new GlobalSettings.MailSettings.SmtpSettings
                {
                    Host = "localhost",
                    TrustServer = true,
                    Port = 10250,
                },
                ReplyToEmail = "noreply@bitwarden.com",
            },
            SiteName = "Bitwarden",
        };

        var mailDeliveryService = new MailKitSmtpMailDeliveryService(globalSettings, Substitute.For<ILogger<MailKitSmtpMailDeliveryService>>());
        var distributedCache = Substitute.For<IDistributedCache>();
        var logger = Substitute.For<ILogger<HandlebarsMailService>>();

        var handlebarsService = new HandlebarsMailService(globalSettings, mailDeliveryService, new BlockingMailEnqueuingService(), distributedCache, logger);

        var sendMethods = typeof(IMailService).GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => m.Name.StartsWith("Send") && m.Name != "SendEnqueuedMailMessageAsync");

        foreach (var sendMethod in sendMethods)
        {
            await InvokeMethod(sendMethod);
        }

        async Task InvokeMethod(MethodInfo method)
        {
            var parameters = method.GetParameters();
            var args = new object[parameters.Length];

            for (var i = 0; i < parameters.Length; i++)
            {
                if (!namedParameters.TryGetValue((parameters[i].Name, parameters[i].ParameterType), out var value))
                {
                    throw new InvalidOperationException($"Couldn't find a parameter for name '{parameters[i].Name}' and type '{parameters[i].ParameterType.FullName}'");
                }

                args[i] = value;
            }

            await (Task)method.Invoke(handlebarsService, args);
        }
    }

    [Fact]
    public async Task SendSendEmailOtpEmailAsync_SendsEmail()
    {
        // Arrange
        var email = "test@example.com";
        var token = "aToken";
        var subject = string.Format("Your Bitwarden Send verification code is {0}", token);

        // Act
        await _sut.SendSendEmailOtpEmailAsync(email, token, subject);

        // Assert
        await _mailDeliveryService.Received(1).SendEmailAsync(Arg.Any<MailMessage>());
    }

    [Fact]
    public async Task SendIndividualUserWelcomeEmailAsync_SendsCorrectEmail()
    {
        // Arrange
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "test@example.com"
        };

        // Act
        await _sut.SendIndividualUserWelcomeEmailAsync(user);

        // Assert
        await _mailDeliveryService.Received(1).SendEmailAsync(Arg.Is<MailMessage>(m =>
            m.MetaData != null &&
            m.ToEmails.Contains("test@example.com") &&
            m.Subject == "Welcome to Bitwarden!" &&
            m.Category == "Welcome"));
    }

    [Fact]
    public async Task SendOrganizationUserWelcomeEmailAsync_SendsCorrectEmailWithOrganizationName()
    {
        // Arrange
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "user@company.com"
        };
        var organizationName = "Bitwarden Corp";

        // Act
        await _sut.SendOrganizationUserWelcomeEmailAsync(user, organizationName);

        // Assert
        await _mailDeliveryService.Received(1).SendEmailAsync(Arg.Is<MailMessage>(m =>
            m.MetaData != null &&
            m.ToEmails.Contains("user@company.com") &&
            m.Subject == "Welcome to Bitwarden!" &&
            m.HtmlContent.Contains("Bitwarden Corp") &&
            m.Category == "Welcome"));
    }

    [Fact]
    public async Task SendFreeOrgOrFamilyOrgUserWelcomeEmailAsync_SendsCorrectEmailWithFamilyTemplate()
    {
        // Arrange
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "family@example.com"
        };
        var familyOrganizationName = "Smith Family";

        // Act
        await _sut.SendFreeOrgOrFamilyOrgUserWelcomeEmailAsync(user, familyOrganizationName);

        // Assert
        await _mailDeliveryService.Received(1).SendEmailAsync(Arg.Is<MailMessage>(m =>
            m.MetaData != null &&
            m.ToEmails.Contains("family@example.com") &&
            m.Subject == "Welcome to Bitwarden!" &&
            m.HtmlContent.Contains("Smith Family") &&
            m.Category == "Welcome"));
    }

    [Theory]
    [InlineData("Acme Corp", "Acme Corp")]
    [InlineData("Company & Associates", "Company &amp; Associates")]
    [InlineData("Test \"Quoted\" Org", "Test &quot;Quoted&quot; Org")]
    public async Task SendOrganizationUserWelcomeEmailAsync_SanitizesOrganizationNameForEmail(string inputOrgName, string expectedSanitized)
    {
        // Arrange
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "test@example.com"
        };

        // Act
        await _sut.SendOrganizationUserWelcomeEmailAsync(user, inputOrgName);

        // Assert
        await _mailDeliveryService.Received(1).SendEmailAsync(Arg.Is<MailMessage>(m =>
            m.HtmlContent.Contains(expectedSanitized) &&
            !m.HtmlContent.Contains("<script>") && // Ensure script tags are removed
            m.Category == "Welcome"));
    }

    [Theory]
    [InlineData("test@example.com")]
    [InlineData("user+tag@domain.co.uk")]
    [InlineData("admin@organization.org")]
    public async Task SendIndividualUserWelcomeEmailAsync_HandlesVariousEmailFormats(string email)
    {
        // Arrange
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = email
        };

        // Act
        await _sut.SendIndividualUserWelcomeEmailAsync(user);

        // Assert
        await _mailDeliveryService.Received(1).SendEmailAsync(Arg.Is<MailMessage>(m =>
            m.ToEmails.Contains(email)));
    }
}
