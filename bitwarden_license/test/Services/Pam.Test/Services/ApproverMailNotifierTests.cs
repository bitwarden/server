using Bit.Core;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.Entities;
using Bit.Core.Pam.Models.Mail.AccessRequestPending;
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
public class ApproverMailNotifierTests
{
    private const string _vaultUrl = "https://vault.example.com/#";
    private const string _organizationName = "Contoso";
    private const string _requesterEmail = "requester@example.com";

    [Theory, BitAutoData]
    public async Task NotifyPendingRequestAsync_FlagOff_SendsNothingAndReadsNothing(AccessRequest request)
    {
        var sutProvider = Setup(flagOn: false);

        await sutProvider.Sut.NotifyPendingRequestAsync(request);

        await sutProvider.GetDependency<ICollectionRepository>().DidNotReceiveWithAnyArgs()
            .GetManagingUserIdsAsync(default);
        await sutProvider.GetDependency<IAccessMailNotifier>().DidNotReceiveWithAnyArgs()
            .SendToUsersAsync(default!, (Func<string, BaseMail<AccessRequestPendingView>>)default!);
    }

    [Theory, BitAutoData]
    public async Task NotifyPendingRequestAsync_SendsOneMailNamingTheRequesterAndTheWindow(
        AccessRequest request, Guid approverId)
    {
        request.NotBefore = new DateTime(2026, 9, 1, 8, 30, 0, DateTimeKind.Utc);
        request.NotAfter = new DateTime(2026, 9, 1, 17, 0, 0, DateTimeKind.Utc);

        var sutProvider = Setup();
        SetupApprovers(sutProvider, request, approverId);
        var recorder = RecordMail(sutProvider);

        await sutProvider.Sut.NotifyPendingRequestAsync(request);

        var (recipients, mail) = Assert.Single(recorder.Pending);
        Assert.Equal(new[] { approverId }, recipients);
        Assert.Equal(_requesterEmail, mail.View.RequesterEmail);
        Assert.Equal(_organizationName, mail.View.OrganizationName);
        Assert.Equal("1 Sep 2026 at 08:30 UTC", mail.View.WindowStart);
        Assert.Equal("1 Sep 2026 at 17:00 UTC", mail.View.WindowEnd);
        Assert.Equal($"{_vaultUrl}/pam/requests/{request.Id}", mail.View.Url);
    }

    [Theory, BitAutoData]
    public async Task NotifyPendingRequestAsync_EveryApproverOfTheCollectionIsMailed(
        AccessRequest request, Guid firstApprover, Guid secondApprover)
    {
        var sutProvider = Setup();
        SetupApprovers(sutProvider, request, firstApprover, secondApprover);
        var recorder = RecordMail(sutProvider);

        await sutProvider.Sut.NotifyPendingRequestAsync(request);

        var (recipients, _) = Assert.Single(recorder.Pending);
        Assert.Equal(new[] { firstApprover, secondApprover }, recipients);
    }

    /// <summary>
    /// An org Owner manages every collection when AllowAdminAccessToAllCollectionItems is on, so a requester is
    /// routinely among their own collection's managers — and DecideAccessRequestCommand refuses a self-decision.
    /// </summary>
    [Theory, BitAutoData]
    public async Task NotifyPendingRequestAsync_RequesterManagesTheCollection_IsNotMailedTheirOwnRequest(
        AccessRequest request, Guid approverId)
    {
        var sutProvider = Setup();
        SetupApprovers(sutProvider, request, approverId, request.RequesterId);
        var recorder = RecordMail(sutProvider);

        await sutProvider.Sut.NotifyPendingRequestAsync(request);

        var (recipients, _) = Assert.Single(recorder.Pending);
        Assert.Equal(new[] { approverId }, recipients);
    }

    [Theory, BitAutoData]
    public async Task NotifyPendingRequestAsync_RequesterIsTheOnlyManager_SendsNothing(AccessRequest request)
    {
        var sutProvider = Setup();
        SetupApprovers(sutProvider, request, request.RequesterId);
        var recorder = RecordMail(sutProvider);

        await sutProvider.Sut.NotifyPendingRequestAsync(request);

        Assert.Empty(recorder.Pending);
    }

    [Theory, BitAutoData]
    public async Task NotifyPendingRequestAsync_RequestsInQuickSuccession_EachGetsItsOwnMail(
        AccessRequest request, Guid approverId)
    {
        var sutProvider = Setup();
        SetupApprovers(sutProvider, request, approverId);
        var recorder = RecordMail(sutProvider);

        var requestIds = new List<Guid>();
        for (var i = 0; i < 12; i++)
        {
            request.Id = Guid.NewGuid();
            requestIds.Add(request.Id);
            await sutProvider.Sut.NotifyPendingRequestAsync(request);
        }

        Assert.Equal(requestIds, recorder.Pending.Select(sent => sent.Mail.View.AccessRequestId));
        Assert.All(recorder.Pending, sent => Assert.Equal(new[] { approverId }, sent.Recipients));
    }

    [Theory, BitAutoData]
    public async Task NotifyPendingRequestAsync_NoApprovers_SendsNothing(AccessRequest request)
    {
        var sutProvider = Setup();
        SetupApprovers(sutProvider, request);
        var recorder = RecordMail(sutProvider);

        await sutProvider.Sut.NotifyPendingRequestAsync(request);

        Assert.Empty(recorder.Pending);
    }

    [Theory, BitAutoData]
    public async Task NotifyPendingRequestAsync_UnknownOrganization_SendsNothing(
        AccessRequest request, Guid approverId)
    {
        var sutProvider = Setup();
        SetupApprovers(sutProvider, request, approverId);
        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(request.OrganizationId)
            .Returns((Organization?)null);
        var recorder = RecordMail(sutProvider);

        await sutProvider.Sut.NotifyPendingRequestAsync(request);

        Assert.Empty(recorder.Pending);
    }

    [Theory, BitAutoData]
    public async Task NotifyPendingRequestAsync_SendFails_DoesNotPropagate(AccessRequest request, Guid approverId)
    {
        var sutProvider = Setup();
        SetupApprovers(sutProvider, request, approverId);
        sutProvider.GetDependency<IAccessMailNotifier>()
            .SendToUsersAsync(Arg.Any<IEnumerable<Guid>>(),
                Arg.Any<Func<string, BaseMail<AccessRequestPendingView>>>())
            .ThrowsAsync(new InvalidOperationException("delivery service unavailable"));

        var exception = await Record.ExceptionAsync(() => sutProvider.Sut.NotifyPendingRequestAsync(request));

        Assert.Null(exception);
    }

    [Theory, BitAutoData]
    public async Task NotifyPendingRequestAsync_ApproverReadFails_DoesNotPropagate(AccessRequest request)
    {
        var sutProvider = Setup();
        sutProvider.GetDependency<ICollectionRepository>().GetManagingUserIdsAsync(request.CollectionId)
            .ThrowsAsync(new TimeoutException("database unavailable"));

        var exception = await Record.ExceptionAsync(() => sutProvider.Sut.NotifyPendingRequestAsync(request));

        Assert.Null(exception);
    }

    private static SutProvider<ApproverMailNotifier> Setup(bool flagOn = true)
    {
        var sutProvider = new SutProvider<ApproverMailNotifier>().Create();

        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.Pam)
            .Returns(flagOn);
        sutProvider.GetDependency<IGlobalSettings>().BaseServiceUri.VaultWithHash.Returns(_vaultUrl);

        return sutProvider;
    }

    private static void SetupApprovers(
        SutProvider<ApproverMailNotifier> sutProvider, AccessRequest request, params Guid[] approverIds)
    {
        sutProvider.GetDependency<ICollectionRepository>().GetManagingUserIdsAsync(request.CollectionId)
            .Returns(approverIds);
        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(request.OrganizationId)
            .Returns(new Organization { Id = request.OrganizationId, Name = _organizationName });
        sutProvider.GetDependency<IUserRepository>().GetByIdAsync(request.RequesterId)
            .Returns(new User { Id = request.RequesterId, Email = _requesterEmail });
    }

    private static MailRecorder RecordMail(SutProvider<ApproverMailNotifier> sutProvider)
    {
        var recorder = new MailRecorder();
        var notifier = sutProvider.GetDependency<IAccessMailNotifier>();

        RecordInto<AccessRequestPendingView, AccessRequestPendingMail>(notifier, recorder.Pending);

        return recorder;
    }

    private static void RecordInto<TView, TMail>(
        IAccessMailNotifier notifier, List<(List<Guid> Recipients, TMail Mail)> sink)
        where TView : BaseMailView
        where TMail : BaseMail<TView> =>
        notifier.When(x => x.SendToUsersAsync(
                Arg.Any<IEnumerable<Guid>>(),
                Arg.Any<Func<string, BaseMail<TView>>>()))
            .Do(call => sink.Add((
                call.Arg<IEnumerable<Guid>>().ToList(),
                (TMail)call.Arg<Func<string, BaseMail<TView>>>()("a@example.com"))));

    private sealed class MailRecorder
    {
        public List<(List<Guid> Recipients, AccessRequestPendingMail Mail)> Pending { get; } = [];
    }
}
