using System.Collections.Concurrent;
using System.Text;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.Auth.Enums;
using Bit.Core.Auth.Models.Data;
using Bit.Core.Auth.Sso.Mail;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Models.Mail;
using Bit.Core.Platform.Mail.Delivery;
using Bit.Core.Platform.Mail.Mailer;
using Bit.Core.Repositories;
using Bit.Core.Settings;
using Bit.Core.Utilities;
using Bit.Sso.IntegrationTest.Utilities;
using Bit.Sso.Utilities;
using Bit.Sso.Utilities.Saml2;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Primitives;
using NSubstitute;
using Sustainsys.Saml2.AspNetCore2;
using Xunit;

namespace Bit.Sso.IntegrationTest.Endpoints;

/// <summary>
/// Uses a <c>Saml2Options</c> object.
/// Does not build the <c>Saml2Options</c> object by hand. Proves that
/// the check for the encrypted-assertion key-transport algorithm is practically reachable,
/// and that it sends the RSA 1.5 deprecation email.
/// </summary>
public class Saml2AssertionKeyTransportAlgorithmVerificationTests
{
    private const string IdpEntityId = "https://idp.example.com";
    private const string RsaOaepMgf1p = "http://www.w3.org/2001/04/xmlenc#rsa-oaep-mgf1p";
    private const string Rsa15 = "http://www.w3.org/2001/04/xmlenc#rsa-1_5";

    private static readonly TimeSpan _wait = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan _negativeWait = TimeSpan.FromMilliseconds(250);

    /// <summary>
    /// Seeds a Confirmed Owner and Admin, and other users that are not eligible. The Confirmed User
    /// is the subject of the assertion. It proves that one email goes to exactly the Confirmed Owner
    /// and the Confirmed Admin of the authenticating organization.
    /// </summary>
    [Fact]
    public async Task CouldHandleAsync_WithRsa15KeyTransport_SendsEmailToConfirmedOwnersAndAdminsOnly()
    {
        // Arrange
        var userEmail = NewEmail("subject");
        using var arrangement = await ArrangeAsync(
            Saml2AcsPostHarness.BuildEncryptedAssertion(Rsa15) + Saml2AcsPostHarness.BuildSubjectAssertion(userEmail),
            configure: MockedMailerWithFlagOn);
        var (samlOptions, organizationId, context, factory) = arrangement;

        var ownerEmail = NewEmail("owner");
        var adminEmail = NewEmail("admin");
        await SeedOrganizationUserAsync(
            factory, organizationId, OrganizationUserType.Owner, OrganizationUserStatusType.Confirmed, ownerEmail);
        await SeedOrganizationUserAsync(
            factory, organizationId, OrganizationUserType.Admin, OrganizationUserStatusType.Confirmed, adminEmail);
        await SeedOrganizationUserAsync(
            factory, organizationId, OrganizationUserType.User, OrganizationUserStatusType.Confirmed, userEmail);
        await SeedOrganizationUserAsync(
            factory, organizationId, OrganizationUserType.Custom, OrganizationUserStatusType.Confirmed, NewEmail("custom"));
        await SeedOrganizationUserAsync(
            factory, organizationId, OrganizationUserType.Admin, OrganizationUserStatusType.Invited, NewEmail("invited"));
        await SeedOrganizationUserAsync(
            factory, organizationId, OrganizationUserType.Admin, OrganizationUserStatusType.Revoked, NewEmail("revoked"));
        var otherOrganizationId = await SeedOrganizationAsync(factory);
        await SeedOrganizationUserAsync(
            factory, otherOrganizationId, OrganizationUserType.Owner, OrganizationUserStatusType.Confirmed,
            NewEmail("other-owner"));
        var probe = new MailProbe(arrangement.Mailer);

        // Act
        await samlOptions.CouldHandleAsync(organizationId.ToString(), context);
        await probe.Sent;
        await Task.Delay(_negativeWait);

        // Assert
        await arrangement.Mailer.Received(1).SendEmail(Arg.Any<Saml2Rsa15DeprecationMail>());
        Assert.Equal(
            new[] { adminEmail, ownerEmail }.Order(StringComparer.OrdinalIgnoreCase),
            probe.Mail!.ToEmails.Order(StringComparer.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Posts two RSA 1.5 requests for the same organization, one after the other. It proves that
    /// the second request in the same interval sends no second email.
    /// </summary>
    [Fact]
    public async Task CouldHandleAsync_SecondRsa15RequestWithinInterval_DoesNotSendAgain()
    {
        // Arrange
        using var arrangement = await ArrangeAsync(
            Saml2AcsPostHarness.BuildEncryptedAssertion(Rsa15), configure: MockedMailerWithFlagOn);
        var (samlOptions, organizationId, context, factory) = arrangement;
        await SeedOrganizationUserAsync(
            factory, organizationId, OrganizationUserType.Owner, OrganizationUserStatusType.Confirmed, NewEmail("owner"));
        var probe = new MailProbe(arrangement.Mailer);

        // Act
        await samlOptions.CouldHandleAsync(organizationId.ToString(), context);
        await probe.Sent;
        await samlOptions.CouldHandleAsync(organizationId.ToString(), context);
        await Task.Delay(_negativeWait);

        // Assert
        await arrangement.Mailer.Received(1).SendEmail(Arg.Any<Saml2Rsa15DeprecationMail>());
    }

    /// <summary>
    /// Posts 10 RSA 1.5 requests for the same organization at the same time. It proves that
    /// the host sends one email.
    /// </summary>
    [Fact]
    public async Task CouldHandleAsync_ConcurrentRsa15Requests_SendOneEmail()
    {
        // Arrange
        using var arrangement = await ArrangeAsync(
            Saml2AcsPostHarness.BuildEncryptedAssertion(Rsa15), configure: MockedMailerWithFlagOn);
        var (samlOptions, organizationId, context, factory) = arrangement;
        await SeedOrganizationUserAsync(
            factory, organizationId, OrganizationUserType.Owner, OrganizationUserStatusType.Confirmed, NewEmail("owner"));
        var probe = new MailProbe(arrangement.Mailer);

        // Act
        await Task.WhenAll(Enumerable.Range(0, 10)
            .Select(_ => Task.Run(() => samlOptions.CouldHandleAsync(organizationId.ToString(), context))));
        await probe.Sent;
        await Task.Delay(_negativeWait);

        // Assert
        await arrangement.Mailer.Received(1).SendEmail(Arg.Any<Saml2Rsa15DeprecationMail>());
    }

    /// <summary>
    /// Posts an assertion with an accepted key-transport algorithm. It proves that the host sends no email.
    /// </summary>
    [Fact]
    public async Task CouldHandleAsync_WithAcceptedKeyTransportAlgorithm_DoesNotSendEmail()
    {
        // Arrange
        using var arrangement = await ArrangeAsync(
            Saml2AcsPostHarness.BuildEncryptedAssertion(RsaOaepMgf1p), configure: MockedMailerWithFlagOn);
        var (samlOptions, organizationId, context, factory) = arrangement;
        await SeedOrganizationUserAsync(
            factory, organizationId, OrganizationUserType.Owner, OrganizationUserStatusType.Confirmed, NewEmail("owner"));

        // Act
        await samlOptions.CouldHandleAsync(organizationId.ToString(), context);
        await Task.Delay(_negativeWait);

        // Assert
        await arrangement.Mailer.DidNotReceive().SendEmail(Arg.Any<Saml2Rsa15DeprecationMail>());
    }

    /// <summary>
    /// Posts an assertion that is not encrypted. It proves that the host sends no email.
    /// </summary>
    [Fact]
    public async Task CouldHandleAsync_WithNoEncryptedAssertions_DoesNotSendEmail()
    {
        // Arrange
        using var arrangement = await ArrangeAsync(
            "<saml:Assertion ID=\"_assertion\"><saml:Issuer>idp</saml:Issuer></saml:Assertion>",
            configure: MockedMailerWithFlagOn);
        var (samlOptions, organizationId, context, factory) = arrangement;
        await SeedOrganizationUserAsync(
            factory, organizationId, OrganizationUserType.Owner, OrganizationUserStatusType.Confirmed, NewEmail("owner"));

        // Act
        await samlOptions.CouldHandleAsync(organizationId.ToString(), context);
        await Task.Delay(_negativeWait);

        // Assert
        await arrangement.Mailer.DidNotReceive().SendEmail(Arg.Any<Saml2Rsa15DeprecationMail>());
    }

    /// <summary>
    /// Posts an RSA 1.5 assertion from an issuer that is not the configured identity provider.
    /// It proves that the host sends no email.
    /// </summary>
    [Fact]
    public async Task CouldHandleAsync_WithMismatchedIssuer_DoesNotSendEmail()
    {
        // Arrange: The issuer does not match the seeded IdpEntityId value. The entity-ID guard
        // in CouldHandleAsync must reject the request before the key transport algorithm verification logic runs.
        using var arrangement = await ArrangeAsync(
            Saml2AcsPostHarness.BuildEncryptedAssertion(Rsa15),
            issuer: "https://not-the-configured-idp.example.com",
            configure: MockedMailerWithFlagOn);
        var (samlOptions, organizationId, context, factory) = arrangement;
        await SeedOrganizationUserAsync(
            factory, organizationId, OrganizationUserType.Owner, OrganizationUserStatusType.Confirmed, NewEmail("owner"));

        // Act
        await samlOptions.CouldHandleAsync(organizationId.ToString(), context);
        await Task.Delay(_negativeWait);

        // Assert
        await arrangement.Mailer.DidNotReceive().SendEmail(Arg.Any<Saml2Rsa15DeprecationMail>());
    }

    /// <summary>
    /// Posts an RSA 1.5 assertion to a cloud host with the feature flag off. It proves that the host sends no email.
    /// </summary>
    [Fact]
    public async Task CouldHandleAsync_CloudWithFlagOff_DoesNotSendEmail()
    {
        // Arrange
        using var arrangement = await ArrangeAsync(
            Saml2AcsPostHarness.BuildEncryptedAssertion(Rsa15),
            configure: b => b.WithMockedMailer().WithRsa15DeprecationEmailFlag(false));
        var (samlOptions, organizationId, context, factory) = arrangement;
        await SeedOrganizationUserAsync(
            factory, organizationId, OrganizationUserType.Owner, OrganizationUserStatusType.Confirmed, NewEmail("owner"));

        // Act
        await samlOptions.CouldHandleAsync(organizationId.ToString(), context);
        await Task.Delay(_negativeWait);

        // Assert
        await arrangement.Mailer.DidNotReceive().SendEmail(Arg.Any<Saml2Rsa15DeprecationMail>());
    }

    /// <summary>
    /// Posts an RSA 1.5 assertion to a self-hosted host with the feature flag off. It proves that
    /// the host sends the email.
    /// </summary>
    [Fact]
    public async Task CouldHandleAsync_SelfHostedWithFlagOff_SendsEmail()
    {
        // Arrange
        using var arrangement = await ArrangeAsync(
            Saml2AcsPostHarness.BuildEncryptedAssertion(Rsa15),
            configure: b => b.WithMockedMailer().WithRsa15DeprecationEmailFlag(false).AsSelfHosted());
        var (samlOptions, organizationId, context, factory) = arrangement;
        await SeedOrganizationUserAsync(
            factory, organizationId, OrganizationUserType.Owner, OrganizationUserStatusType.Confirmed, NewEmail("owner"));
        var probe = new MailProbe(arrangement.Mailer);

        // Act
        await samlOptions.CouldHandleAsync(organizationId.ToString(), context);

        // Assert
        await probe.Sent;
    }

    /// <summary>
    /// Makes the mailer throw when it sends. It proves that <c>CouldHandleAsync</c> still returns true
    /// and that the mailer received the email.
    /// </summary>
    [Fact]
    public async Task CouldHandleAsync_MailerThrows_StillReturnsTrue()
    {
        // Arrange
        using var arrangement = await ArrangeAsync(
            Saml2AcsPostHarness.BuildEncryptedAssertion(Rsa15), configure: MockedMailerWithFlagOn);
        var (samlOptions, organizationId, context, factory) = arrangement;
        await SeedOrganizationUserAsync(
            factory, organizationId, OrganizationUserType.Owner, OrganizationUserStatusType.Confirmed, NewEmail("owner"));
        var probe = new MailProbe(arrangement.Mailer, new InvalidOperationException("simulated mailer failure"));

        // Act
        var result = await samlOptions.CouldHandleAsync(organizationId.ToString(), context);
        await probe.Sent;

        // Assert
        Assert.True(result);
    }

    /// <summary>
    /// Uses the real mailer and a real SMTP delivery service, and posts an RSA 1.5 assertion. It proves that
    /// the delivery service receives exactly one message, that the SMTP send completes without error, and that
    /// the message goes to the Owner and Admin addresses. A developer reads the rendered email in MailCatcher.
    /// </summary>
    [Fact(Skip = "For local development - requires MailCatcher at localhost:10250")]
    public async Task CouldHandleAsync_WithRsa15KeyTransport_DeliversEmailToMailCatcher()
    {
        // Arrange
        var globalSettings = new GlobalSettings
        {
            Mail = new GlobalSettings.MailSettings
            {
                ReplyToEmail = "no-reply@bitwarden.com",
                Smtp = new GlobalSettings.MailSettings.SmtpSettings
                {
                    Host = "localhost",
                    Port = 10250,
                    StartTls = false,
                    Ssl = false
                }
            },
            SiteName = "Bitwarden"
        };
        var deliveryService = new SignalingMailDeliveryService(
            new MailKitSmtpMailDeliveryService(globalSettings, NullLogger<MailKitSmtpMailDeliveryService>.Instance));
        using var arrangement = await ArrangeAsync(
            Saml2AcsPostHarness.BuildEncryptedAssertion(Rsa15),
            configure: b => b.WithRsa15DeprecationEmailFlag().WithMailDeliveryService(deliveryService));
        var (samlOptions, organizationId, context, factory) = arrangement;

        var ownerEmail = NewEmail("owner");
        var adminEmail = NewEmail("admin");
        await SeedOrganizationUserAsync(
            factory, organizationId, OrganizationUserType.Owner, OrganizationUserStatusType.Confirmed, ownerEmail);
        await SeedOrganizationUserAsync(
            factory, organizationId, OrganizationUserType.Admin, OrganizationUserStatusType.Confirmed, adminEmail);

        // Act
        await samlOptions.CouldHandleAsync(organizationId.ToString(), context);
        await deliveryService.Completed.WaitAsync(TimeSpan.FromSeconds(10));

        // Assert
        var message = Assert.Single(deliveryService.Messages);
        Assert.Equal(
            new[] { adminEmail, ownerEmail }.Order(StringComparer.OrdinalIgnoreCase),
            message.ToEmails.Order(StringComparer.OrdinalIgnoreCase));
    }

    private static SsoTestDataBuilder MockedMailerWithFlagOn(SsoTestDataBuilder builder) =>
        builder.WithMockedMailer().WithRsa15DeprecationEmailFlag();

    private static string NewEmail(string label) => $"{label}_{Guid.NewGuid()}@test.com";

    private static async Task<Arrangement> ArrangeAsync(
        string assertionElement,
        string issuer = IdpEntityId,
        Func<SsoTestDataBuilder, SsoTestDataBuilder>? configure = null)
    {
        var builder = new SsoTestDataBuilder()
            .WithSsoConfig(cfg => cfg!.SetData(new SsoConfigurationData
            {
                ConfigType = SsoType.Saml2,
                IdpEntityId = IdpEntityId,
                IdpSingleSignOnServiceUrl = "https://idp.example.com/sso",
                IdpX509PublicCert = CoreHelpers.Base64UrlEncode(Saml2AcsPostHarness.BuildIdpCertificate().RawData),
            }));
        var testData = await (configure?.Invoke(builder) ?? builder).BuildAsync();

        var organizationId = testData.Organization!.Id;
        var scheme = await testData.Factory.Services.GetRequiredService<IAuthenticationSchemeProvider>()
            .GetSchemeAsync(organizationId.ToString());
        var dynamicScheme = Assert.IsType<DynamicAuthenticationScheme>(scheme);
        var samlOptions = Assert.IsType<Saml2Options>(dynamicScheme.Options);

        var responseXml = Saml2AcsPostHarness.BuildResponseXml(assertionElement, issuer);
        var context = new DefaultHttpContext
        {
            RequestServices = testData.Factory.Services.CreateScope().ServiceProvider,
        };
        context.Request.Path = SsoConfigurationData.BuildSaml2AcsUrl(null, organizationId.ToString());
        context.Request.Method = HttpMethods.Post;
        context.Request.ContentType = "application/x-www-form-urlencoded";
        context.Request.Form = new FormCollection(new Dictionary<string, StringValues>
        {
            ["SAMLResponse"] = Convert.ToBase64String(Encoding.UTF8.GetBytes(responseXml)),
        });

        return new Arrangement(samlOptions, organizationId, context, testData.Factory);
    }

    private static async Task<Guid> SeedOrganizationAsync(SsoApplicationFactory factory)
    {
        var organization = await factory.Services.GetRequiredService<IOrganizationRepository>()
            .CreateAsync(new Organization
            {
                Id = Guid.NewGuid(),
                Name = "Other Organization",
                BillingEmail = "billing@test.com",
                Plan = "Enterprise",
                Enabled = true,
            });
        return organization.Id;
    }

    private static async Task SeedOrganizationUserAsync(
        SsoApplicationFactory factory,
        Guid organizationId,
        OrganizationUserType type,
        OrganizationUserStatusType status,
        string email)
    {
        var organizationUser = new OrganizationUser
        {
            OrganizationId = organizationId,
            Status = status,
            Type = type,
        };

        if (status == OrganizationUserStatusType.Invited)
        {
            organizationUser.Email = email;
        }
        else
        {
            var user = await factory.Services.GetRequiredService<IUserRepository>().CreateAsync(new User
            {
                Email = email,
                Name = "TestUser",
                ApiKey = Guid.NewGuid().ToString(),
                SecurityStamp = Guid.NewGuid().ToString(),
            });
            organizationUser.UserId = user.Id;
        }

        await factory.Services.GetRequiredService<IOrganizationUserRepository>().CreateAsync(organizationUser);
    }

    // Disposing this disposes the host, so a test does not leak it.
    private sealed record Arrangement(
        Saml2Options SamlOptions, Guid OrganizationId, HttpContext Context, SsoApplicationFactory Factory) : IDisposable
    {
        public IMailer Mailer => Factory.Services.GetRequiredService<IMailer>();

        public void Dispose() => Factory.Dispose();
    }

    private sealed class MailProbe
    {
        private readonly TaskCompletionSource _sent = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public MailProbe(IMailer mailer, Exception? throwOnSend = null)
        {
            mailer.When(m => m.SendEmail(Arg.Any<Saml2Rsa15DeprecationMail>())).Do(callInfo =>
            {
                Mail = callInfo.Arg<Saml2Rsa15DeprecationMail>();
                _sent.TrySetResult();
                if (throwOnSend != null)
                {
                    throw throwOnSend;
                }
            });
        }

        public Saml2Rsa15DeprecationMail? Mail { get; private set; }

        public Task Sent => _sent.Task.WaitAsync(_wait);
    }

    private sealed class SignalingMailDeliveryService(IMailDeliveryService inner) : IMailDeliveryService
    {
        private readonly TaskCompletionSource _completed = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly ConcurrentQueue<MailMessage> _messages = new();

        public IReadOnlyCollection<MailMessage> Messages => _messages;

        public Task Completed => _completed.Task;

        public async Task SendEmailAsync(MailMessage message)
        {
            _messages.Enqueue(message);
            try
            {
                await inner.SendEmailAsync(message);
                _completed.TrySetResult();
            }
            catch (Exception ex)
            {
                _completed.TrySetException(ex);
                throw;
            }
        }
    }
}
