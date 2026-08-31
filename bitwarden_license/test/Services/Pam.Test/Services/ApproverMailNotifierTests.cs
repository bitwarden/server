using Bit.Core;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.Entities;
using Bit.Core.Pam.Models.Mail.AccessRequestPending;
using Bit.Core.Pam.Models.Mail.AccessRequestsWaiting;
using Bit.Core.Platform.Mail.Mailer;
using Bit.Core.Repositories;
using Bit.Core.Settings;
using Bit.Pam.Entities;
using Bit.Services.Pam.Services;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Bitwarden.Server.Sdk.Features;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Time.Testing;
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

    private static readonly DateTime _now = new(2026, 8, 31, 9, 0, 0, DateTimeKind.Utc);

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
    public async Task NotifyPendingRequestAsync_FlagOff_ClaimsNoBurstWindow(AccessRequest request, Guid approverId)
    {
        var cache = new RecordingCache();
        var sutProvider = Setup(flagOn: false, cache: cache);
        SetupApprovers(sutProvider, request, approverId);

        await sutProvider.Sut.NotifyPendingRequestAsync(request);

        Assert.Empty(cache.Entries);
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
        Assert.Empty(recorder.Waiting);
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
        Assert.Empty(recorder.Waiting);
    }

    [Theory, BitAutoData]
    public async Task NotifyPendingRequestAsync_BelowTheThreshold_EachRequestGetsItsOwnMail(
        AccessRequest request, Guid approverId)
    {
        var sutProvider = Setup();
        SetupApprovers(sutProvider, request, approverId);
        var recorder = RecordMail(sutProvider);

        for (var i = 0; i < ApproverMailNotifier.BurstThreshold; i++)
        {
            await sutProvider.Sut.NotifyPendingRequestAsync(request);
        }

        Assert.Equal(ApproverMailNotifier.BurstThreshold, recorder.Pending.Count);
        Assert.Empty(recorder.Waiting);
    }

    [Theory, BitAutoData]
    public async Task NotifyPendingRequestAsync_CrossingTheThreshold_CollapsesIntoOneMail(
        AccessRequest request, Guid approverId)
    {
        var sutProvider = Setup();
        SetupApprovers(sutProvider, request, approverId);
        var recorder = RecordMail(sutProvider);

        for (var i = 0; i < ApproverMailNotifier.BurstThreshold + 4; i++)
        {
            await sutProvider.Sut.NotifyPendingRequestAsync(request);
        }

        Assert.Equal(ApproverMailNotifier.BurstThreshold, recorder.Pending.Count);
        var (recipients, collapsed) = Assert.Single(recorder.Waiting);
        Assert.Equal(new[] { approverId }, recipients);
        Assert.Equal(ApproverMailNotifier.BurstThreshold + 1, collapsed.View.RequestCount);
        Assert.Equal(ApproverMailNotifier.BurstWindowMinutes, collapsed.View.WindowMinutes);
        Assert.Equal($"{_vaultUrl}/pam/approvals", collapsed.View.Url);
    }

    [Theory, BitAutoData]
    public async Task NotifyPendingRequestAsync_WindowElapsed_TheCounterResets(
        AccessRequest request, Guid approverId)
    {
        var sutProvider = Setup();
        SetupApprovers(sutProvider, request, approverId);
        var recorder = RecordMail(sutProvider);

        for (var i = 0; i < ApproverMailNotifier.BurstThreshold + 2; i++)
        {
            await sutProvider.Sut.NotifyPendingRequestAsync(request);
        }

        sutProvider.GetDependency<FakeTimeProvider>()
            .SetUtcNow(_now.AddMinutes(ApproverMailNotifier.BurstWindowMinutes));
        await sutProvider.Sut.NotifyPendingRequestAsync(request);

        Assert.Equal(ApproverMailNotifier.BurstThreshold + 1, recorder.Pending.Count);
        Assert.Single(recorder.Waiting);
    }

    /// <summary>
    /// The window is bounded by the clock, not by the cache entry's lifetime, so an entry that outlives its window
    /// on a backend that evicts late still starts a fresh window rather than suppressing forever.
    /// </summary>
    [Theory, BitAutoData]
    public async Task NotifyPendingRequestAsync_ExpiryIsPinnedToTheWindowStart(AccessRequest request, Guid approverId)
    {
        var cache = new RecordingCache();
        var sutProvider = Setup(cache: cache);
        SetupApprovers(sutProvider, request, approverId);

        await sutProvider.Sut.NotifyPendingRequestAsync(request);
        sutProvider.GetDependency<FakeTimeProvider>().SetUtcNow(_now.AddMinutes(5));
        await sutProvider.Sut.NotifyPendingRequestAsync(request);

        var expected = new DateTimeOffset(_now, TimeSpan.Zero).AddMinutes(ApproverMailNotifier.BurstWindowMinutes);
        Assert.All(cache.Expirations, expiry => Assert.Equal(expected, expiry));
    }

    /// <summary>
    /// Fail open. The breaker is a courtesy to the approver's inbox, so a cache outage must degrade to unthrottled
    /// mail rather than to silence — silence would leave requesters blocked on approvers who were never told.
    /// </summary>
    [Theory, BitAutoData]
    public async Task NotifyPendingRequestAsync_BurstWindowUnreadable_KeepsSendingPastTheThreshold(
        AccessRequest request, Guid approverId)
    {
        var sutProvider = Setup(cache: new ThrowingCache());
        SetupApprovers(sutProvider, request, approverId);
        var recorder = RecordMail(sutProvider);

        const int sends = ApproverMailNotifier.BurstThreshold + 3;
        for (var i = 0; i < sends; i++)
        {
            await sutProvider.Sut.NotifyPendingRequestAsync(request);
        }

        Assert.Equal(sends, recorder.Pending.Count);
        Assert.Empty(recorder.Waiting);
    }

    [Theory, BitAutoData]
    public async Task NotifyPendingRequestAsync_NoApprovers_SendsNothing(AccessRequest request)
    {
        var sutProvider = Setup();
        SetupApprovers(sutProvider, request);
        var recorder = RecordMail(sutProvider);

        await sutProvider.Sut.NotifyPendingRequestAsync(request);

        Assert.Empty(recorder.Pending);
        Assert.Empty(recorder.Waiting);
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

    private static SutProvider<ApproverMailNotifier> Setup(bool flagOn = true, IDistributedCache? cache = null)
    {
        var sutProvider = new SutProvider<ApproverMailNotifier>()
            .WithFakeTimeProvider()
            .SetDependency(cache ?? new RecordingCache())
            .Create();

        sutProvider.GetDependency<FakeTimeProvider>().SetUtcNow(_now);
        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.PamEmailNotifications)
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
        RecordInto<AccessRequestsWaitingView, AccessRequestsWaitingMail>(notifier, recorder.Waiting);

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
        public List<(List<Guid> Recipients, AccessRequestsWaitingMail Mail)> Waiting { get; } = [];
    }

    /// <summary>
    /// Never evicts. The notifier decides the window from <see cref="TimeProvider" /> rather than from the entry
    /// still being present, so a cache that ignores expiry is the strictest fixture for the reset specs.
    /// </summary>
    private class RecordingCache : IDistributedCache
    {
        public Dictionary<string, byte[]> Entries { get; } = [];
        public List<DateTimeOffset?> Expirations { get; } = [];

        public byte[]? Get(string key) => Entries.GetValueOrDefault(key);

        public virtual Task<byte[]?> GetAsync(string key, CancellationToken token = default) => Task.FromResult(Get(key));

        public void Refresh(string key) { }

        public Task RefreshAsync(string key, CancellationToken token = default) => Task.CompletedTask;

        public void Remove(string key) => Entries.Remove(key);

        public Task RemoveAsync(string key, CancellationToken token = default)
        {
            Remove(key);
            return Task.CompletedTask;
        }

        public void Set(string key, byte[] value, DistributedCacheEntryOptions options)
        {
            Entries[key] = value;
            Expirations.Add(options.AbsoluteExpiration);
        }

        public Task SetAsync(
            string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default)
        {
            Set(key, value, options);
            return Task.CompletedTask;
        }
    }

    private sealed class ThrowingCache : RecordingCache
    {
        public override Task<byte[]?> GetAsync(string key, CancellationToken token = default) =>
            Task.FromException<byte[]?>(new InvalidOperationException("cache unavailable"));
    }
}
