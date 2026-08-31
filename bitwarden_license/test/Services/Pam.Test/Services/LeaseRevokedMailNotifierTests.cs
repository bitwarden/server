using Bit.Core;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.Pam.Models.Mail.AccessLeaseRevoked;
using Bit.Core.Platform.Mail.Mailer;
using Bit.Core.Repositories;
using Bit.Core.Settings;
using Bit.Pam.Entities;
using Bit.Pam.Enums;
using Bit.Services.Pam.Services;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Bitwarden.Server.Sdk.Features;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace Bit.Services.Pam.Test.Services;

[SutProviderCustomize]
public class LeaseRevokedMailNotifierTests
{
    private const string _vaultUrl = "https://vault.example.com/#";
    private const string _organizationName = "Contoso";

    [Theory, BitAutoData]
    public async Task NotifyLeaseEndedAsync_Revoked_MailsTheHolderWithTheWindowItCutShort(AccessLease lease)
    {
        lease.NotAfter = new DateTime(2026, 9, 1, 17, 0, 0, DateTimeKind.Utc);

        var sutProvider = Setup();
        SetupOrganization(sutProvider, lease);
        var sent = RecordMail(sutProvider);

        await sutProvider.Sut.NotifyLeaseEndedAsync(lease, AccessLeaseAction.Revoked);

        var (recipientId, mail) = Assert.Single(sent);
        Assert.Equal(lease.RequesterId, recipientId);
        Assert.Equal("Your access was ended", mail.Subject);
        Assert.Equal(_organizationName, mail.View.OrganizationName);
        Assert.Equal("1 Sep 2026 at 17:00 UTC", mail.View.ScheduledEnd);
        Assert.Equal($"{_vaultUrl}/privileged-controls/requests/{lease.AccessRequestId}", mail.View.Url);
    }

    /// <summary>
    /// The point of the whole feature. A holder who ends their own access already knows, and a mail thirty seconds
    /// behind their own click is what teaches people to filter the channel.
    /// </summary>
    [Theory]
    [BitAutoData(AccessLeaseAction.Cancelled)]
    [BitAutoData(AccessLeaseAction.None)]
    public async Task NotifyLeaseEndedAsync_NotRevoked_ReadsNothingAndSendsNothing(
        AccessLeaseAction endAction, AccessLease lease)
    {
        var sutProvider = Setup();
        SetupOrganization(sutProvider, lease);
        var sent = RecordMail(sutProvider);

        await sutProvider.Sut.NotifyLeaseEndedAsync(lease, endAction);

        Assert.Empty(sent);
        await sutProvider.GetDependency<IAccessMailNotifier>().DidNotReceiveWithAnyArgs()
            .SendToUserAsync(default, (Func<string, BaseMail<AccessLeaseRevokedView>>)default!);
        await sutProvider.GetDependency<IOrganizationRepository>().DidNotReceiveWithAnyArgs().GetByIdAsync(default);
    }

    [Theory, BitAutoData]
    public async Task NotifyLeaseEndedAsync_FlagOff_ReadsNothingAndSendsNothing(AccessLease lease)
    {
        var sutProvider = Setup(flagOn: false);

        await sutProvider.Sut.NotifyLeaseEndedAsync(lease, AccessLeaseAction.Revoked);

        await sutProvider.GetDependency<IOrganizationRepository>().DidNotReceiveWithAnyArgs().GetByIdAsync(default);
        await sutProvider.GetDependency<IAccessMailNotifier>().DidNotReceiveWithAnyArgs()
            .SendToUserAsync(default, (Func<string, BaseMail<AccessLeaseRevokedView>>)default!);
    }

    [Theory, BitAutoData]
    public async Task NotifyLeaseEndedAsync_MailsTheHolderAndNobodyElse(AccessLease lease)
    {
        var sutProvider = Setup();
        SetupOrganization(sutProvider, lease);
        var sent = RecordMail(sutProvider);

        await sutProvider.Sut.NotifyLeaseEndedAsync(lease, AccessLeaseAction.Revoked);

        var (recipientId, _) = Assert.Single(sent);
        Assert.Equal(lease.RequesterId, recipientId);
        await sutProvider.GetDependency<IAccessMailNotifier>().DidNotReceiveWithAnyArgs()
            .SendToUsersAsync(default!, (Func<string, BaseMail<AccessLeaseRevokedView>>)default!);
    }

    [Theory, BitAutoData]
    public async Task NotifyLeaseEndedAsync_UnknownOrganization_SendsNothing(AccessLease lease)
    {
        var sutProvider = Setup();
        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(lease.OrganizationId)
            .Returns((Organization?)null);
        var sent = RecordMail(sutProvider);

        await sutProvider.Sut.NotifyLeaseEndedAsync(lease, AccessLeaseAction.Revoked);

        Assert.Empty(sent);
    }

    [Theory, BitAutoData]
    public async Task NotifyLeaseEndedAsync_SendFails_DoesNotPropagate(AccessLease lease)
    {
        var sutProvider = Setup();
        SetupOrganization(sutProvider, lease);
        sutProvider.GetDependency<IAccessMailNotifier>()
            .SendToUserAsync(Arg.Any<Guid>(), Arg.Any<Func<string, BaseMail<AccessLeaseRevokedView>>>())
            .ThrowsAsync(new InvalidOperationException("delivery service unavailable"));

        var exception = await Record.ExceptionAsync(
            () => sutProvider.Sut.NotifyLeaseEndedAsync(lease, AccessLeaseAction.Revoked));

        Assert.Null(exception);
    }

    [Theory, BitAutoData]
    public async Task NotifyLeaseEndedAsync_OrganizationReadFails_DoesNotPropagate(AccessLease lease)
    {
        var sutProvider = Setup();
        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(lease.OrganizationId)
            .ThrowsAsync(new TimeoutException("database unavailable"));

        var exception = await Record.ExceptionAsync(
            () => sutProvider.Sut.NotifyLeaseEndedAsync(lease, AccessLeaseAction.Revoked));

        Assert.Null(exception);
    }

    private static SutProvider<LeaseRevokedMailNotifier> Setup(bool flagOn = true)
    {
        var sutProvider = new SutProvider<LeaseRevokedMailNotifier>().Create();

        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.PamEmailNotifications)
            .Returns(flagOn);
        sutProvider.GetDependency<IGlobalSettings>().BaseServiceUri.VaultWithHash.Returns(_vaultUrl);

        return sutProvider;
    }

    private static void SetupOrganization(SutProvider<LeaseRevokedMailNotifier> sutProvider, AccessLease lease) =>
        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(lease.OrganizationId)
            .Returns(new Organization { Id = lease.OrganizationId, Name = _organizationName });

    private static List<(Guid RecipientId, AccessLeaseRevokedMail Mail)> RecordMail(
        SutProvider<LeaseRevokedMailNotifier> sutProvider)
    {
        List<(Guid RecipientId, AccessLeaseRevokedMail Mail)> sent = [];

        sutProvider.GetDependency<IAccessMailNotifier>()
            .When(x => x.SendToUserAsync(
                Arg.Any<Guid>(),
                Arg.Any<Func<string, BaseMail<AccessLeaseRevokedView>>>()))
            .Do(call => sent.Add((
                call.Arg<Guid>(),
                (AccessLeaseRevokedMail)call.Arg<Func<string, BaseMail<AccessLeaseRevokedView>>>()(
                    "holder@example.com"))));

        return sent;
    }
}
