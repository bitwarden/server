using System.Globalization;
using System.Text;
using Bit.Core.Auth.Sso;
using Bit.Core.Auth.Sso.Mail;
using Bit.Core.Enums;
using Bit.Core.Models.Data.Organizations.OrganizationUsers;
using Bit.Core.Platform.Mail.Delivery;
using Bit.Core.Platform.Mail.Mailer;
using Bit.Core.Repositories;
using Bit.Core.Settings;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace Bit.Core.Test.Auth.Sso;

[SutProviderCustomize]
public class Saml2Rsa15DeprecationNoticeCommandTests
{
    private const string Recipient = "admin@example.com";

    private static SutProvider<Saml2Rsa15DeprecationNoticeCommand> BuildSut(
        int cooldownInDays = 14, IMailDeliveryService? mailDeliveryService = null)
    {
        var globalSettings = Substitute.For<IGlobalSettings>();
        var ssoSettings = Substitute.For<ISsoSettings>();
        ssoSettings.Rsa15DeprecationEmailCooldownInDays = cooldownInDays;
        globalSettings.Sso.Returns(ssoSettings);

        var sutProvider = new SutProvider<Saml2Rsa15DeprecationNoticeCommand>()
            .SetDependency(globalSettings)
            .WithFakeTimeProvider();
        if (mailDeliveryService is not null)
        {
            sutProvider.SetDependency(mailDeliveryService);
        }

        return sutProvider.Create();
    }

    private static void SetupAdmins(
        SutProvider<Saml2Rsa15DeprecationNoticeCommand> sutProvider,
        Guid organizationId,
        params string?[] emails) =>
        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyByMinimumRoleAsync(organizationId, OrganizationUserType.Admin)
            .Returns(emails.Select(e => new OrganizationUserUserDetails { Email = e! }).ToArray());

    private static byte[] EncodeTimestamp(long ticks) =>
        Encoding.UTF8.GetBytes(ticks.ToString(CultureInfo.InvariantCulture));

    private static void SetStoredValue(
        SutProvider<Saml2Rsa15DeprecationNoticeCommand> sutProvider, Guid organizationId, byte[]? value) =>
        sutProvider.GetDependency<IDistributedCache>()
            .GetAsync(KeyFor(organizationId), Arg.Any<CancellationToken>())
            .Returns(value);

    private static string KeyFor(Guid organizationId) => $"sso:saml2:rsa15-deprecation-email:{organizationId}";

    private static async Task AssertEmailSentAsync(SutProvider<Saml2Rsa15DeprecationNoticeCommand> sutProvider) =>
        await sutProvider.GetDependency<IMailer>()
            .Received(1)
            .SendEmail(Arg.Any<Saml2Rsa15DeprecationMail>());

    private static async Task AssertNoEmailSentAsync(SutProvider<Saml2Rsa15DeprecationNoticeCommand> sutProvider) =>
        await sutProvider.GetDependency<IMailer>()
            .DidNotReceiveWithAnyArgs()
            .SendEmail<Saml2Rsa15DeprecationMailView>(default!);

    private static async Task AssertEmailSendCooldownStartedAsync(
        SutProvider<Saml2Rsa15DeprecationNoticeCommand> sutProvider, Guid organizationId, long nowTicks) =>
        await sutProvider.GetDependency<IDistributedCache>().Received(1).SetAsync(
            KeyFor(organizationId),
            Arg.Is<byte[]>(b => b.SequenceEqual(EncodeTimestamp(nowTicks))),
            Arg.Any<DistributedCacheEntryOptions>(),
            Arg.Any<CancellationToken>());

    private static async Task AssertEmailSendCooldownRemovedAsync(
        SutProvider<Saml2Rsa15DeprecationNoticeCommand> sutProvider, Guid organizationId) =>
        await sutProvider.GetDependency<IDistributedCache>().Received(1).RemoveAsync(
            KeyFor(organizationId),
            Arg.Any<CancellationToken>());

    private static async Task AssertEmailSendCooldownNotRemovedAsync(
        SutProvider<Saml2Rsa15DeprecationNoticeCommand> sutProvider) =>
        await sutProvider.GetDependency<IDistributedCache>().DidNotReceiveWithAnyArgs().RemoveAsync(
            default!, default);

    private static async Task AssertNoCacheWriteAsync(SutProvider<Saml2Rsa15DeprecationNoticeCommand> sutProvider) =>
        await sutProvider.GetDependency<IDistributedCache>().DidNotReceiveWithAnyArgs().SetAsync(
            default!, default!, default!, default);

    [Theory, BitAutoData]
    public async Task SendEmailIfDueAsync_NoCooldownEntry_StartsEmailSendCooldownAndSendsEmail(Guid organizationId)
    {
        var sutProvider = BuildSut();
        var nowTicks = sutProvider.GetDependency<FakeTimeProvider>().GetUtcNow().UtcTicks;
        SetStoredValue(sutProvider, organizationId, null);
        SetupAdmins(sutProvider, organizationId, Recipient);

        await sutProvider.Sut.SendEmailIfDueAsync(organizationId);

        await AssertEmailSendCooldownStartedAsync(sutProvider, organizationId, nowTicks);
        await AssertEmailSentAsync(sutProvider);
        await AssertEmailSendCooldownNotRemovedAsync(sutProvider);
    }

    [Theory, BitAutoData]
    public async Task SendEmailIfDueAsync_EntryWithinCooldown_SendsNothingAndDoesNotWrite(Guid organizationId)
    {
        var sutProvider = BuildSut(14);
        var now = sutProvider.GetDependency<FakeTimeProvider>().GetUtcNow();
        SetStoredValue(sutProvider, organizationId, EncodeTimestamp(now.AddDays(-13).UtcTicks));
        SetupAdmins(sutProvider, organizationId, Recipient);

        await sutProvider.Sut.SendEmailIfDueAsync(organizationId);

        await AssertNoCacheWriteAsync(sutProvider);
        await AssertNoEmailSentAsync(sutProvider);
        await sutProvider.GetDependency<IOrganizationUserRepository>()
            .DidNotReceiveWithAnyArgs()
            .GetManyByMinimumRoleAsync(default, default);
    }

    [Theory]
    [BitAutoData(14)]
    [BitAutoData(15)]
    public async Task SendEmailIfDueAsync_EntryAtOrOlderThanCooldown_StartsEmailSendCooldownAndSendsEmail(
        int elapsedDays, Guid organizationId)
    {
        var sutProvider = BuildSut(14);
        var now = sutProvider.GetDependency<FakeTimeProvider>().GetUtcNow();
        SetStoredValue(sutProvider, organizationId, EncodeTimestamp(now.AddDays(-elapsedDays).UtcTicks));
        SetupAdmins(sutProvider, organizationId, Recipient);

        await sutProvider.Sut.SendEmailIfDueAsync(organizationId);

        await AssertEmailSendCooldownStartedAsync(sutProvider, organizationId, now.UtcTicks);
        await AssertEmailSentAsync(sutProvider);
    }

    [Theory, BitAutoData]
    public async Task SendEmailIfDueAsync_CooldownShortenedBelowElapsedTime_SendsEmail(Guid organizationId)
    {
        var sutProvider = BuildSut(7);
        var now = sutProvider.GetDependency<FakeTimeProvider>().GetUtcNow();
        SetStoredValue(sutProvider, organizationId, EncodeTimestamp(now.AddDays(-10).UtcTicks));
        SetupAdmins(sutProvider, organizationId, Recipient);

        await sutProvider.Sut.SendEmailIfDueAsync(organizationId);

        await AssertEmailSendCooldownStartedAsync(sutProvider, organizationId, now.UtcTicks);
        await AssertEmailSentAsync(sutProvider);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task SendEmailIfDueAsync_CooldownZeroOrLess_SendsNothingWithoutCacheCall(int cooldownInDays)
    {
        var sutProvider = BuildSut(cooldownInDays);
        var organizationId = Guid.NewGuid();
        SetupAdmins(sutProvider, organizationId, Recipient);

        await sutProvider.Sut.SendEmailIfDueAsync(organizationId);

        var cache = sutProvider.GetDependency<IDistributedCache>();
        await cache.DidNotReceiveWithAnyArgs().GetAsync(default!, default);
        await AssertNoCacheWriteAsync(sutProvider);
        await AssertNoEmailSentAsync(sutProvider);
    }

    [Fact]
    public async Task SendEmailIfDueAsync_MailDeliveryNotConfigured_SendsNothingAndUsesNoCacheOrRepository()
    {
        var sutProvider = BuildSut(mailDeliveryService: new NoopMailDeliveryService());
        var organizationId = Guid.NewGuid();
        SetupAdmins(sutProvider, organizationId, Recipient);

        await sutProvider.Sut.SendEmailIfDueAsync(organizationId);

        var cache = sutProvider.GetDependency<IDistributedCache>();
        await cache.DidNotReceiveWithAnyArgs().GetAsync(default!, default);
        await AssertNoCacheWriteAsync(sutProvider);
        await sutProvider.GetDependency<IOrganizationUserRepository>()
            .DidNotReceiveWithAnyArgs()
            .GetManyByMinimumRoleAsync(default, default);
        await AssertNoEmailSentAsync(sutProvider);
    }

    [Theory, BitAutoData]
    public async Task SendEmailIfDueAsync_UnparseableEntry_SendsEmail(Guid organizationId)
    {
        var sutProvider = BuildSut();
        SetStoredValue(sutProvider, organizationId, Encoding.UTF8.GetBytes("not-a-number"));
        SetupAdmins(sutProvider, organizationId, Recipient);

        await sutProvider.Sut.SendEmailIfDueAsync(organizationId);

        await AssertEmailSentAsync(sutProvider);
    }

    [Theory]
    [InlineData(14)]
    [InlineData(7)]
    public async Task SendEmailIfDueAsync_WritesExpiryEqualToCooldownAndKeyWithOrganizationId(int cooldownInDays)
    {
        var organizationId = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var sutProvider = BuildSut(cooldownInDays);
        SetupAdmins(sutProvider, organizationId, Recipient);

        await sutProvider.Sut.SendEmailIfDueAsync(organizationId);

        await sutProvider.GetDependency<IDistributedCache>().Received(1).SetAsync(
            "sso:saml2:rsa15-deprecation-email:00000000-0000-0000-0000-000000000001",
            Arg.Any<byte[]>(),
            Arg.Is<DistributedCacheEntryOptions>(o =>
                o.AbsoluteExpirationRelativeToNow == TimeSpan.FromDays(cooldownInDays)),
            Arg.Any<CancellationToken>());
    }

    [Theory, BitAutoData]
    public async Task SendEmailIfDueAsync_CacheThrows_DoesNotPropagateAndSendsNothing(Guid organizationId)
    {
        var sutProvider = BuildSut();
        sutProvider.GetDependency<IDistributedCache>()
            .GetAsync(KeyFor(organizationId), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("cache failure"));
        SetupAdmins(sutProvider, organizationId, Recipient);

        await sutProvider.Sut.SendEmailIfDueAsync(organizationId);

        await AssertNoEmailSentAsync(sutProvider);
        await AssertEmailSendCooldownNotRemovedAsync(sutProvider);
    }

    [Theory, BitAutoData]
    public async Task SendEmailIfDueAsync_OwnersAndAdmins_SendsOneMailToAllAddresses(Guid organizationId)
    {
        var sutProvider = BuildSut();
        string[] addresses = ["owner@example.com", "admin@example.com", "other@example.com"];
        SetupAdmins(sutProvider, organizationId, addresses);

        await sutProvider.Sut.SendEmailIfDueAsync(organizationId);

        await sutProvider.GetDependency<IMailer>()
            .Received(1)
            .SendEmail(Arg.Is<Saml2Rsa15DeprecationMail>(mail => mail.ToEmails.SequenceEqual(addresses)));
    }

    [Theory, BitAutoData]
    public async Task SendEmailIfDueAsync_QueriesTheAdminMinimumRoleForTheGivenOrganization(Guid organizationId)
    {
        var sutProvider = BuildSut();
        SetupAdmins(sutProvider, organizationId, Recipient);

        await sutProvider.Sut.SendEmailIfDueAsync(organizationId);

        await sutProvider.GetDependency<IOrganizationUserRepository>()
            .Received(1)
            .GetManyByMinimumRoleAsync(organizationId, OrganizationUserType.Admin);
    }

    [Theory, BitAutoData]
    public async Task SendEmailIfDueAsync_NullAndBlankEmails_AreDropped(Guid organizationId)
    {
        var sutProvider = BuildSut();
        SetupAdmins(sutProvider, organizationId, null, "", "   ", Recipient);

        await sutProvider.Sut.SendEmailIfDueAsync(organizationId);

        await sutProvider.GetDependency<IMailer>()
            .Received(1)
            .SendEmail(Arg.Is<Saml2Rsa15DeprecationMail>(mail =>
                mail.ToEmails.SequenceEqual(new[] { Recipient })));
    }

    [Theory, BitAutoData]
    public async Task SendEmailIfDueAsync_DuplicateEmailsIgnoringCase_AreSentOnce(Guid organizationId)
    {
        var sutProvider = BuildSut();
        SetupAdmins(sutProvider, organizationId, "a@x.com", "A@X.COM", "b@x.com");

        await sutProvider.Sut.SendEmailIfDueAsync(organizationId);

        await sutProvider.GetDependency<IMailer>()
            .Received(1)
            .SendEmail(Arg.Is<Saml2Rsa15DeprecationMail>(mail =>
                mail.ToEmails.SequenceEqual(new[] { "a@x.com", "b@x.com" })));
    }

    [Theory, BitAutoData]
    public async Task SendEmailIfDueAsync_NoRecipients_SendsNothingAndKeepsEmailSendCooldown(Guid organizationId)
    {
        var sutProvider = BuildSut();
        SetupAdmins(sutProvider, organizationId);

        await sutProvider.Sut.SendEmailIfDueAsync(organizationId);

        SetupAdmins(sutProvider, organizationId, null, "", "   ");

        await sutProvider.Sut.SendEmailIfDueAsync(organizationId);

        await AssertNoEmailSentAsync(sutProvider);
        await AssertEmailSendCooldownNotRemovedAsync(sutProvider);
    }

    [Theory, BitAutoData]
    public async Task SendEmailIfDueAsync_RepositoryThrows_DoesNotPropagateAndRemovesEmailSendCooldown(Guid organizationId)
    {
        var sutProvider = BuildSut();
        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyByMinimumRoleAsync(organizationId, OrganizationUserType.Admin)
            .ThrowsAsync(new InvalidOperationException("repository failure"));

        await sutProvider.Sut.SendEmailIfDueAsync(organizationId);

        await AssertNoEmailSentAsync(sutProvider);
        await AssertEmailSendCooldownRemovedAsync(sutProvider, organizationId);
    }

    [Theory, BitAutoData]
    public async Task SendEmailIfDueAsync_MailerThrows_RemovesEmailSendCooldownAndLogsFixedMessageWithoutOrganizationId(
        Guid organizationId)
    {
        var sutProvider = BuildSut();
        var nowTicks = sutProvider.GetDependency<FakeTimeProvider>().GetUtcNow().UtcTicks;
        var thrownException = new InvalidOperationException("mailer failure");
        SetupAdmins(sutProvider, organizationId, Recipient);
        sutProvider.GetDependency<IMailer>()
            .SendEmail(Arg.Any<Saml2Rsa15DeprecationMail>())
            .ThrowsAsync(thrownException);

        await sutProvider.Sut.SendEmailIfDueAsync(organizationId);

        await AssertEmailSendCooldownStartedAsync(sutProvider, organizationId, nowTicks);
        await AssertEmailSendCooldownRemovedAsync(sutProvider, organizationId);
        sutProvider.GetDependency<ILogger<Saml2Rsa15DeprecationNoticeCommand>>().Received(1).Log(
            LogLevel.Error,
            Arg.Any<EventId>(),
            Arg.Is<object>(state =>
                state.ToString() == "Failed to send the RSA 1.5 deprecation email." &&
                !state.ToString()!.Contains(organizationId.ToString()) &&
                !state.ToString()!.Contains(Recipient)),
            thrownException,
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Theory, BitAutoData]
    public async Task SendEmailIfDueAsync_MailerAndCooldownRemovalThrow_LogsBothExceptions(Guid organizationId)
    {
        var sutProvider = BuildSut();
        var mailerException = new InvalidOperationException("mailer failure");
        var removalException = new InvalidOperationException("cache removal failure");
        SetupAdmins(sutProvider, organizationId, Recipient);
        sutProvider.GetDependency<IMailer>()
            .SendEmail(Arg.Any<Saml2Rsa15DeprecationMail>())
            .ThrowsAsync(mailerException);
        sutProvider.GetDependency<IDistributedCache>()
            .RemoveAsync(KeyFor(organizationId), Arg.Any<CancellationToken>())
            .ThrowsAsync(removalException);

        await sutProvider.Sut.SendEmailIfDueAsync(organizationId);

        var logger = sutProvider.GetDependency<ILogger<Saml2Rsa15DeprecationNoticeCommand>>();
        logger.Received(1).Log(
            LogLevel.Error,
            Arg.Any<EventId>(),
            Arg.Is<object>(state =>
                state.ToString() == "Failed to remove the RSA 1.5 deprecation email send cooldown." &&
                !state.ToString()!.Contains(organizationId.ToString())),
            removalException,
            Arg.Any<Func<object, Exception?, string>>());
        logger.Received(1).Log(
            LogLevel.Error,
            Arg.Any<EventId>(),
            Arg.Is<object>(state => state.ToString() == "Failed to send the RSA 1.5 deprecation email."),
            mailerException,
            Arg.Any<Func<object, Exception?, string>>());
    }
}
