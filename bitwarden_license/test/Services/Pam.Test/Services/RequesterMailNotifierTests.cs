using Bit.Core;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.Pam.Models.Mail.AccessRequestDecided;
using Bit.Core.Platform.Mail.Mailer;
using Bit.Core.Repositories;
using Bit.Core.Settings;
using Bit.Pam.Entities;
using Bit.Services.Pam.Services;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Bitwarden.Server.Sdk.Features;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace Bit.Services.Pam.Test.Services;

[SutProviderCustomize]
public class RequesterMailNotifierTests
{
    private const string _vaultUrl = "https://vault.example.com/#";
    private const string _organizationName = "Contoso";

    [Theory, BitAutoData]
    public async Task NotifyDecisionAsync_FlagOff_ReadsNothingAndSendsNothing(AccessRequest request)
    {
        var sutProvider = Setup(flagOn: false);

        await sutProvider.Sut.NotifyDecisionAsync(request, approved: true);

        await sutProvider.GetDependency<IOrganizationRepository>().DidNotReceiveWithAnyArgs().GetByIdAsync(default);
        await sutProvider.GetDependency<IAccessMailNotifier>().DidNotReceiveWithAnyArgs()
            .SendToUserAsync(default, (Func<string, BaseMail<AccessRequestDecidedView>>)default!);
    }

    [Theory, BitAutoData]
    public async Task NotifyDecisionAsync_Approved_SendsTheApprovedVerdictAndTheWindow(AccessRequest request)
    {
        request.NotBefore = new DateTime(2026, 9, 1, 8, 30, 0, DateTimeKind.Utc);
        request.NotAfter = new DateTime(2026, 9, 1, 17, 0, 0, DateTimeKind.Utc);

        var sutProvider = Setup();
        SetupOrganization(sutProvider, request);
        var sent = RecordMail(sutProvider);

        await sutProvider.Sut.NotifyDecisionAsync(request, approved: true);

        var (recipientId, mail) = Assert.Single(sent);
        Assert.Equal(request.RequesterId, recipientId);
        Assert.True(mail.View.Approved);
        Assert.Equal("Your access request was approved", mail.Subject);
        Assert.Equal(_organizationName, mail.View.OrganizationName);
        Assert.Equal("1 Sep 2026 at 08:30 UTC", mail.View.WindowStart);
        Assert.Equal("1 Sep 2026 at 17:00 UTC", mail.View.WindowEnd);
        Assert.Equal($"{_vaultUrl}/privileged-controls/requests/{request.Id}", mail.View.Url);
    }

    [Theory, BitAutoData]
    public async Task NotifyDecisionAsync_Denied_SendsTheDeniedVerdictToTheSameAddress(AccessRequest request)
    {
        var sutProvider = Setup();
        SetupOrganization(sutProvider, request);
        var sent = RecordMail(sutProvider);

        await sutProvider.Sut.NotifyDecisionAsync(request, approved: false);

        var (recipientId, mail) = Assert.Single(sent);
        Assert.Equal(request.RequesterId, recipientId);
        Assert.False(mail.View.Approved);
        Assert.Equal("Your access request was denied", mail.Subject);
        Assert.Equal($"{_vaultUrl}/privileged-controls/requests/{request.Id}", mail.View.Url);
    }

    /// <summary>
    /// The approver is the actor, not an audience: they pressed the button and already know the outcome. The
    /// notifier is given no approver identity at all, so the only recipient it can reach is the requester.
    /// </summary>
    [Theory, BitAutoData]
    public async Task NotifyDecisionAsync_MailsTheRequesterAndNobodyElse(AccessRequest request)
    {
        var sutProvider = Setup();
        SetupOrganization(sutProvider, request);
        var sent = RecordMail(sutProvider);

        await sutProvider.Sut.NotifyDecisionAsync(request, approved: true);

        var (recipientId, _) = Assert.Single(sent);
        Assert.Equal(request.RequesterId, recipientId);
        await sutProvider.GetDependency<IAccessMailNotifier>().DidNotReceiveWithAnyArgs()
            .SendToUsersAsync(default!, (Func<string, BaseMail<AccessRequestDecidedView>>)default!);
    }

    [Theory, BitAutoData]
    public async Task NotifyDecisionAsync_UnknownOrganization_SendsNothing(AccessRequest request)
    {
        var sutProvider = Setup();
        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(request.OrganizationId)
            .Returns((Organization?)null);
        var sent = RecordMail(sutProvider);

        await sutProvider.Sut.NotifyDecisionAsync(request, approved: true);

        Assert.Empty(sent);
    }

    [Theory, BitAutoData]
    public async Task NotifyDecisionAsync_SendFails_DoesNotPropagate(AccessRequest request)
    {
        var sutProvider = Setup();
        SetupOrganization(sutProvider, request);
        sutProvider.GetDependency<IAccessMailNotifier>()
            .SendToUserAsync(Arg.Any<Guid>(), Arg.Any<Func<string, BaseMail<AccessRequestDecidedView>>>())
            .ThrowsAsync(new InvalidOperationException("delivery service unavailable"));

        var exception = await Record.ExceptionAsync(() => sutProvider.Sut.NotifyDecisionAsync(request, approved: true));

        Assert.Null(exception);
    }

    [Theory, BitAutoData]
    public async Task NotifyDecisionAsync_OrganizationReadFails_DoesNotPropagate(AccessRequest request)
    {
        var sutProvider = Setup();
        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(request.OrganizationId)
            .ThrowsAsync(new TimeoutException("database unavailable"));

        var exception = await Record.ExceptionAsync(() => sutProvider.Sut.NotifyDecisionAsync(request, approved: false));

        Assert.Null(exception);
    }

    private static SutProvider<RequesterMailNotifier> Setup(bool flagOn = true)
    {
        var sutProvider = new SutProvider<RequesterMailNotifier>().Create();

        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.PamEmailNotifications)
            .Returns(flagOn);
        sutProvider.GetDependency<IGlobalSettings>().BaseServiceUri.VaultWithHash.Returns(_vaultUrl);

        return sutProvider;
    }

    private static void SetupOrganization(SutProvider<RequesterMailNotifier> sutProvider, AccessRequest request) =>
        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(request.OrganizationId)
            .Returns(new Organization { Id = request.OrganizationId, Name = _organizationName });

    private static List<(Guid RecipientId, AccessRequestDecidedMail Mail)> RecordMail(
        SutProvider<RequesterMailNotifier> sutProvider)
    {
        List<(Guid RecipientId, AccessRequestDecidedMail Mail)> sent = [];

        sutProvider.GetDependency<IAccessMailNotifier>()
            .When(x => x.SendToUserAsync(
                Arg.Any<Guid>(),
                Arg.Any<Func<string, BaseMail<AccessRequestDecidedView>>>()))
            .Do(call => sent.Add((
                call.Arg<Guid>(),
                (AccessRequestDecidedMail)call.Arg<Func<string, BaseMail<AccessRequestDecidedView>>>()(
                    "requester@example.com"))));

        return sent;
    }
}
