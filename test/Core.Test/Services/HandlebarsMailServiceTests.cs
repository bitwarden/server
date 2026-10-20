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
    // CoreHelpers.PreventEmailAutoLinking inserts this after "." and "@" so mail clients
    // do not auto-link organization names that look like domains. Handlebars encodes it as
    // &#8204; in HTML parts; text templates triple-stache the value so it stays raw.
    private const string ZeroWidthNonJoiner = "\u200C";

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

    [Fact]
    public async Task SendOrganizationMaxSeatLimitReachedEmailAsync_RendersOrganizationName()
    {
        // Arrange
        var organization = new Organization { Id = Guid.NewGuid(), Name = "Acme.Corp" };

        // Act
        await _sut.SendOrganizationMaxSeatLimitReachedEmailAsync(organization, 5, new[] { "owner@example.com" });

        // Assert
        await _mailDeliveryService.Received(1).SendEmailAsync(Arg.Is<MailMessage>(m =>
            m.Subject == "Acme.Corp seat limit reached" &&
            m.HtmlContent.Contains("Acme.&#8204;Corp has reached the seat limit of 5") &&
            m.TextContent.Contains($"Acme.{ZeroWidthNonJoiner}Corp has reached the seat limit of 5") &&
            !m.TextContent.Contains("&#8204;") &&
            !m.HtmlContent.Contains("[dot]") &&
            !m.HtmlContent.Contains("Your organization has reached") &&
            m.Category == "OrganizationSeatsMaxReached"));
    }

    [Fact]
    public async Task SendSecretsManagerMaxSeatLimitReachedEmailAsync_RendersOrganizationName()
    {
        // Arrange
        var organization = new Organization { Id = Guid.NewGuid(), Name = "Acme.Corp" };

        // Act
        await _sut.SendSecretsManagerMaxSeatLimitReachedEmailAsync(organization, 5, new[] { "owner@example.com" });

        // Assert
        await _mailDeliveryService.Received(1).SendEmailAsync(Arg.Is<MailMessage>(m =>
            m.Subject == "Acme.Corp Secrets Manager seat limit reached" &&
            m.HtmlContent.Contains("Acme.&#8204;Corp has reached the Secrets Manager seat limit of 5") &&
            m.TextContent.Contains($"Acme.{ZeroWidthNonJoiner}Corp has reached the Secrets Manager seat limit of 5") &&
            !m.HtmlContent.Contains("[dot]") &&
            !m.HtmlContent.Contains("Your organization has reached") &&
            m.Category == "OrganizationSmSeatsMaxReached"));
    }

    [Fact]
    public async Task SendSecretsManagerMaxServiceAccountLimitReachedEmailAsync_RendersOrganizationName()
    {
        // Arrange
        var organization = new Organization { Id = Guid.NewGuid(), Name = "Acme.Corp" };
        var currentYear = DateTime.UtcNow.Year.ToString();

        // Act
        await _sut.SendSecretsManagerMaxServiceAccountLimitReachedEmailAsync(organization, 5, new[] { "owner@example.com" });

        // Assert
        await _mailDeliveryService.Received(1).SendEmailAsync(Arg.Is<MailMessage>(m =>
            m.Subject == "Acme.Corp Secrets Manager machine accounts limit reached" &&
            m.HtmlContent.Contains("Acme.&#8204;Corp has reached the Secrets Manager machine accounts limit of 5") &&
            m.TextContent.Contains($"Acme.{ZeroWidthNonJoiner}Corp has reached the Secrets Manager machine accounts limit of 5") &&
            !m.HtmlContent.Contains("[dot]") &&
            m.HtmlContent.Contains("&copy; " + currentYear + " Bitwarden Inc.") &&
            !m.HtmlContent.Contains("Your organization has reached") &&
            m.Category == "OrganizationSmServiceAccountsMaxReached"));
    }

    [Fact]
    public async Task SendLicenseExpiredAsync_UsesUpdatedSubject()
    {
        // Act
        await _sut.SendLicenseExpiredAsync(new[] { "user@example.com" });

        // Assert
        await _mailDeliveryService.Received(1).SendEmailAsync(Arg.Is<MailMessage>(m =>
            m.Subject == "License expired" &&
            m.Category == "LicenseExpired"));
    }

    [Fact]
    public async Task SendLicenseExpiredAsync_RendersOrganizationName()
    {
        // Act
        await _sut.SendLicenseExpiredAsync(new[] { "user@example.com" }, "Acme.Corp");

        // Assert
        await _mailDeliveryService.Received(1).SendEmailAsync(Arg.Is<MailMessage>(m =>
            m.HtmlContent.Contains("your Bitwarden organization license for <b") &&
            m.HtmlContent.Contains("Acme.&#8204;Corp</b> has expired and must be updated for continued use") &&
            m.TextContent.Contains($"your Bitwarden organization license for Acme.{ZeroWidthNonJoiner}Corp has expired and must be updated for continued use") &&
            !m.HtmlContent.Contains("[dot]") &&
            !m.TextContent.Contains("[dot]") &&
            m.Category == "LicenseExpired"));
    }

    [Fact]
    public async Task SendProviderUpdatePaymentMethod_RendersUpdatedCopy()
    {
        // Act
        await _sut.SendProviderUpdatePaymentMethod(Guid.NewGuid(), "Acme.Corp", "Best.MSP", new[] { "owner@example.com" });

        // Assert
        await _mailDeliveryService.Received(1).SendEmailAsync(Arg.Is<MailMessage>(m =>
            m.HtmlContent.Contains("Your Bitwarden organization, Acme.&#8204;Corp, is no longer managed by Best.&#8204;MSP.") &&
            !m.HtmlContent.Contains("[dot]") &&
            m.HtmlContent.Contains("going to Admin Console in the web app, then selecting your organization, Billing, and") &&
            m.HtmlContent.Contains(">Payment Details</a>") &&
            m.HtmlContent.Contains("/billing/payment-details") &&
            !m.HtmlContent.Contains("/billing/payment-method") &&
            m.HtmlContent.Contains("https://bitwarden.com/help/update-billing-info/#update-billing-for-organizations") &&
            m.TextContent.Contains($"Your Bitwarden organization, Acme.{ZeroWidthNonJoiner}Corp, is no longer managed by Best.{ZeroWidthNonJoiner}MSP.") &&
            !m.TextContent.Contains("[dot]") &&
            m.TextContent.Contains("Or click the following link:") &&
            m.TextContent.Contains("/billing/payment-details") &&
            !m.TextContent.Contains("<a ") &&
            m.Category == "ProviderUpdatePaymentMethod"));
    }

    [Theory]
    [InlineData(true, "Accept the offer to activate your complimentary plan.")]
    [InlineData(false, "create a Bitwarden account with your personal email address")]
    public async Task SendFamiliesForEnterpriseOfferEmailAsync_RendersUpdatedSubjectAndCopy(bool existingAccount, string expectedCopy)
    {
        // Arrange
        _mailEnqueuingService
            .EnqueueManyAsync(Arg.Any<IEnumerable<IMailQueueMessage>>(), Arg.Any<Func<IMailQueueMessage, Task>>())
            .Returns(callInfo => Task.WhenAll(
                callInfo.Arg<IEnumerable<IMailQueueMessage>>().Select(callInfo.Arg<Func<IMailQueueMessage, Task>>())));

        // Act
        await _sut.SendFamiliesForEnterpriseOfferEmailAsync("Acme.Corp", "user@example.com", existingAccount, "token");

        // Assert
        await _mailDeliveryService.Received(1).SendEmailAsync(Arg.Is<MailMessage>(m =>
            m.Subject == "Accept your Sponsored Families Plan" &&
            m.HtmlContent.Contains("Acme.&#8204;Corp has sponsored a free Families plan for you!") &&
            m.HtmlContent.Contains(expectedCopy) &&
            m.HtmlContent.Contains("If you do not recognize this account, please ignore this message.") &&
            m.HtmlContent.Contains("/accept-families-for-enterprise?token=token") &&
            !m.HtmlContent.Contains("[dot]") &&
            m.TextContent.Contains($"Acme.{ZeroWidthNonJoiner}Corp has sponsored a free Families plan for you!") &&
            m.TextContent.Contains(expectedCopy) &&
            m.TextContent.Contains("If you do not recognize this account, please ignore this message.") &&
            m.TextContent.Contains("/accept-families-for-enterprise?token=token&email=") &&
            !m.TextContent.Contains("&amp;") &&
            m.Category == "FamiliesForEnterpriseOffer"));
    }

    [Fact]
    public async Task SendFamiliesForEnterpriseSponsorshipRevertingEmailAsync_RendersUpdatedCopyWithFormattedDate()
    {
        // Arrange
        var expirationDate = new DateTime(2026, 9, 30);
        var formattedDate = expirationDate.ToString("MMMM dd, yyyy");

        // Act
        await _sut.SendFamiliesForEnterpriseSponsorshipRevertingEmailAsync("user@example.com", expirationDate);

        // Assert
        await _mailDeliveryService.Received(1).SendEmailAsync(Arg.Is<MailMessage>(m =>
            m.Subject == "Your Sponsored Families Plan will be ending" &&
            m.HtmlContent.Contains($"Your Sponsored Families Plan will continue until {formattedDate}") &&
            m.TextContent.Contains($"Your Sponsored Families Plan will continue until {formattedDate}")));
    }

    [Fact]
    public async Task SendFamiliesForEnterpriseRemoveSponsorshipsEmailAsync_RendersUpdatedCopyWithSubscriptionLink()
    {
        // Arrange
        var organizationId = Guid.NewGuid().ToString();

        // Act
        await _sut.SendFamiliesForEnterpriseRemoveSponsorshipsEmailAsync("user@example.com", organizationId, "Acme.Corp");

        // Assert
        await _mailDeliveryService.Received(1).SendEmailAsync(Arg.Is<MailMessage>(m =>
            m.Subject == "Your Sponsored Families Plan has been removed" &&
            m.HtmlContent.Contains("Acme.&#8204;Corp has removed the Sponsored Families Plan. You can no longer redeem this benefit or access an existing family vault.") &&
            !m.HtmlContent.Contains("[dot]") &&
            m.HtmlContent.Contains("Contact your organization admin for more information.") &&
            m.HtmlContent.Contains($"/organizations/{organizationId}/billing/subscription\" target=\"_blank\" clicktracking=off>") &&
            m.TextContent.Contains($"Acme.{ZeroWidthNonJoiner}Corp has removed the Sponsored Families Plan. You can no longer redeem this benefit or access an existing family vault.") &&
            !m.TextContent.Contains("[dot]") &&
            m.TextContent.Contains($"Or click the following link:") &&
            m.TextContent.Contains($"/organizations/{organizationId}/billing/subscription") &&
            m.Category == "FamiliesForEnterpriseRemovedFromFamilyUser"));
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

    [Theory]
    [InlineData("us", "https://vault.bitwarden.com")]
    [InlineData("eu", "https://vault.bitwarden.eu")]
    [InlineData("gov", "https://vault.bitwarden-gov.com")]
    public void GetCloudVaultSubscriptionUrl_ResolvesPerRegion(string cloudRegion, string expectedVaultBase)
    {
        // Arrange
        _globalSettings.BaseServiceUri.CloudRegion = cloudRegion;

        // Act
        var result = _sut.GetCloudVaultSubscriptionUrl(Guid.NewGuid());

        // Assert
        Assert.StartsWith(expectedVaultBase, result);
    }

    [Theory]
    [InlineData(nameof(HandlebarsMailService.SendEmergencyAccessConfirmedEmailAsync))]
    [InlineData(nameof(HandlebarsMailService.SendEmergencyAccessRecoveryApproved))]
    [InlineData(nameof(HandlebarsMailService.SendEmergencyAccessRecoveryReminder))]
    public async Task EmergencyAccessEmails_ShouldEncodeNamesOnlyOnce(string methodName)
    {
        // Arrange
        const string name = "Alice & Bob";
        const string email = "recipient@example.com";
        var emergencyAccess = new EmergencyAccess
        {
            Type = EmergencyAccessType.Takeover,
            RecoveryInitiatedDate = DateTime.UtcNow.AddHours(-1),
            WaitTimeDays = 2,
        };

        // Act
        switch (methodName)
        {
            case nameof(HandlebarsMailService.SendEmergencyAccessConfirmedEmailAsync):
                await _sut.SendEmergencyAccessConfirmedEmailAsync(name, email);
                break;
            case nameof(HandlebarsMailService.SendEmergencyAccessRecoveryApproved):
                await _sut.SendEmergencyAccessRecoveryApproved(emergencyAccess, name, email);
                break;
            case nameof(HandlebarsMailService.SendEmergencyAccessRecoveryReminder):
                await _sut.SendEmergencyAccessRecoveryReminder(emergencyAccess, name, email);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(methodName), methodName, null);
        }

        // Assert
        await _mailDeliveryService.Received(1).SendEmailAsync(Arg.Is<MailMessage>(m =>
            m.HtmlContent.Contains("Alice &amp; Bob") &&
            !m.HtmlContent.Contains("Alice &amp;amp; Bob")));
    }
}
