using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums.Partnerships;
using Bit.Core.AdminConsole.OrganizationFeatures.Partnerships;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Enums;
using Bit.Core.Services;
using Bit.Test.Common.AutoFixture;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Xunit;
using GlobalSettings = Bit.Core.Settings.GlobalSettings;

namespace Bit.Core.Test.AdminConsole.OrganizationFeatures.Partnerships;

public class TransitionPartnershipEntitlementCommandTests
{
    private static readonly DateTime _now = new(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);
    private static readonly TimeSpan _resumeWindow = TimeSpan.FromDays(30);
    private static readonly Guid _boundUserId = Guid.NewGuid();

    private static readonly (PartnershipEntitlementState From, PartnershipEntitlementAction Action, PartnershipEntitlementState To, EventType Event)[] _legalTransitions =
    [
        (PartnershipEntitlementState.Provisioned, PartnershipEntitlementAction.Activate, PartnershipEntitlementState.Active, EventType.PartnershipEntitlement_Activated),
        (PartnershipEntitlementState.Active, PartnershipEntitlementAction.Suspend, PartnershipEntitlementState.Suspended, EventType.PartnershipEntitlement_Suspended),
        (PartnershipEntitlementState.Active, PartnershipEntitlementAction.Cancel, PartnershipEntitlementState.Canceled, EventType.PartnershipEntitlement_Deactivated),
        (PartnershipEntitlementState.Active, PartnershipEntitlementAction.UserExit, PartnershipEntitlementState.Canceled, EventType.PartnershipEntitlement_Deactivated),
        (PartnershipEntitlementState.Suspended, PartnershipEntitlementAction.Resume, PartnershipEntitlementState.Active, EventType.PartnershipEntitlement_Activated),
        (PartnershipEntitlementState.Suspended, PartnershipEntitlementAction.Cancel, PartnershipEntitlementState.Canceled, EventType.PartnershipEntitlement_Deactivated),
        (PartnershipEntitlementState.Canceled, PartnershipEntitlementAction.Resume, PartnershipEntitlementState.Active, EventType.PartnershipEntitlement_Activated),
        (PartnershipEntitlementState.Canceled, PartnershipEntitlementAction.Activate, PartnershipEntitlementState.Active, EventType.PartnershipEntitlement_Activated),
    ];

    public static TheoryData<PartnershipEntitlementState, PartnershipEntitlementAction> LegalTransitions
    {
        get
        {
            var data = new TheoryData<PartnershipEntitlementState, PartnershipEntitlementAction>();
            foreach (var t in _legalTransitions)
            {
                data.Add(t.From, t.Action);
            }
            return data;
        }
    }

    public static TheoryData<PartnershipEntitlementState, PartnershipEntitlementAction, PartnershipEntitlementState> LegalTransitionTargets
    {
        get
        {
            var data = new TheoryData<PartnershipEntitlementState, PartnershipEntitlementAction, PartnershipEntitlementState>();
            foreach (var t in _legalTransitions)
            {
                data.Add(t.From, t.Action, t.To);
            }
            return data;
        }
    }

    public static TheoryData<PartnershipEntitlementState, PartnershipEntitlementAction, EventType> LegalTransitionEvents
    {
        get
        {
            var data = new TheoryData<PartnershipEntitlementState, PartnershipEntitlementAction, EventType>();
            foreach (var t in _legalTransitions)
            {
                data.Add(t.From, t.Action, t.Event);
            }
            return data;
        }
    }

    /// <remarks>
    /// User-driven actions are requested by the bound user. A canceled entitlement is inside its resume window.
    /// </remarks>
    public static TheoryData<PartnershipEntitlementState, PartnershipEntitlementAction, string> IllegalCombinations =>
        new()
        {
            { PartnershipEntitlementState.Provisioned, PartnershipEntitlementAction.Suspend, "illegal_transition" },
            { PartnershipEntitlementState.Provisioned, PartnershipEntitlementAction.Resume, "illegal_transition" },
            { PartnershipEntitlementState.Provisioned, PartnershipEntitlementAction.Cancel, "illegal_transition" },
            { PartnershipEntitlementState.Provisioned, PartnershipEntitlementAction.UserExit, "not_found" },
            { PartnershipEntitlementState.Active, PartnershipEntitlementAction.Activate, "already_bound" },
            { PartnershipEntitlementState.Active, PartnershipEntitlementAction.Resume, "illegal_transition" },
            { PartnershipEntitlementState.Suspended, PartnershipEntitlementAction.Activate, "already_bound" },
            { PartnershipEntitlementState.Suspended, PartnershipEntitlementAction.Suspend, "illegal_transition" },
            { PartnershipEntitlementState.Suspended, PartnershipEntitlementAction.UserExit, "illegal_transition" },
            { PartnershipEntitlementState.Canceled, PartnershipEntitlementAction.Suspend, "illegal_transition" },
            { PartnershipEntitlementState.Canceled, PartnershipEntitlementAction.Cancel, "illegal_transition" },
            { PartnershipEntitlementState.Canceled, PartnershipEntitlementAction.UserExit, "illegal_transition" },
        };

    [Theory]
    [MemberData(nameof(LegalTransitionTargets))]
    public async Task TransitionAsync_LegalTransition_AppliesTargetState(
        PartnershipEntitlementState from, PartnershipEntitlementAction action, PartnershipEntitlementState expected)
    {
        var sutProvider = CreateSutProvider();
        var entitlement = CreateEntitlement(from);
        ArrangePartnership(sutProvider, entitlement);

        var result = await sutProvider.Sut.TransitionAsync(CreateRequest(entitlement, action));

        Assert.True(result.IsSuccess);
        Assert.True(result.AsSuccess.Applied);
        Assert.Equal(expected, result.AsSuccess.Entitlement.State);
    }

    [Theory]
    [MemberData(nameof(LegalTransitions))]
    public async Task TransitionAsync_LegalTransition_SavesEntitlement(
        PartnershipEntitlementState from, PartnershipEntitlementAction action)
    {
        var sutProvider = CreateSutProvider();
        var entitlement = CreateEntitlement(from);
        ArrangePartnership(sutProvider, entitlement);

        await sutProvider.Sut.TransitionAsync(CreateRequest(entitlement, action));

        await sutProvider.GetDependency<IOrganizationPartnershipEntitlementRepository>()
            .Received(1)
            .ReplaceIfUnchangedAsync(Arg.Is<OrganizationPartnershipEntitlement>(e =>
                e == entitlement && e.LastAppliedEffectiveDate == _now && e.RevisionDate == _now), Arg.Any<DateTime>());
    }

    [Theory]
    [MemberData(nameof(LegalTransitionEvents))]
    public async Task TransitionAsync_LegalTransition_LogsLifecycleEventForPartnershipOrganization(
        PartnershipEntitlementState from, PartnershipEntitlementAction action, EventType expectedEvent)
    {
        var sutProvider = CreateSutProvider();
        var entitlement = CreateEntitlement(from);
        var partnership = ArrangePartnership(sutProvider, entitlement);

        await sutProvider.Sut.TransitionAsync(CreateRequest(entitlement, action));

        await sutProvider.GetDependency<IEventService>()
            .Received(1)
            .LogOrganizationPartnershipEventAsync(partnership.OrganizationId, expectedEvent, _now);
    }

    [Theory]
    [MemberData(nameof(IllegalCombinations))]
    public async Task TransitionAsync_IllegalCombination_ReturnsError(
        PartnershipEntitlementState from, PartnershipEntitlementAction action, string expectedCode)
    {
        var sutProvider = CreateSutProvider();
        var entitlement = CreateEntitlement(from);
        ArrangePartnership(sutProvider, entitlement);

        var result = await sutProvider.Sut.TransitionAsync(CreateRequest(entitlement, action));

        Assert.True(result.IsError);
        Assert.Equal(expectedCode, Assert.IsAssignableFrom<IPartnershipError>(result.AsError).Code);
    }

    [Theory]
    [MemberData(nameof(IllegalCombinations))]
    public async Task TransitionAsync_IllegalCombination_DoesNotSave(
        PartnershipEntitlementState from, PartnershipEntitlementAction action, string _)
    {
        var sutProvider = CreateSutProvider();
        var entitlement = CreateEntitlement(from);
        ArrangePartnership(sutProvider, entitlement);

        await sutProvider.Sut.TransitionAsync(CreateRequest(entitlement, action));

        await sutProvider.GetDependency<IOrganizationPartnershipEntitlementRepository>()
            .DidNotReceiveWithAnyArgs()
            .ReplaceIfUnchangedAsync(default!, default);
    }

    [Fact]
    public async Task TransitionAsync_ActivateFromProvisioned_BindsUserAndMintsAccountRef()
    {
        var sutProvider = CreateSutProvider();
        var entitlement = CreateEntitlement(PartnershipEntitlementState.Provisioned);
        ArrangePartnership(sutProvider, entitlement);

        var result = await sutProvider.Sut.TransitionAsync(CreateRequest(entitlement, PartnershipEntitlementAction.Activate));

        var activated = result.AsSuccess.Entitlement;
        Assert.Equal(_boundUserId, activated.UserId);
        Assert.NotNull(activated.AccountRef);
        Assert.Equal(_now, activated.BoundDate);
    }

    [Fact]
    public async Task TransitionAsync_Cancel_HoldsBindingUntilResumeWindowExpires()
    {
        var sutProvider = CreateSutProvider();
        var entitlement = CreateEntitlement(PartnershipEntitlementState.Active);
        var accountRef = entitlement.AccountRef;
        ArrangePartnership(sutProvider, entitlement);
        var effectiveAt = _now.AddHours(-2);

        var result = await sutProvider.Sut.TransitionAsync(
            CreateRequest(entitlement, PartnershipEntitlementAction.Cancel, effectiveAt: effectiveAt));

        var canceled = result.AsSuccess.Entitlement;
        Assert.Equal(effectiveAt, canceled.CanceledDate);
        Assert.Equal(effectiveAt + _resumeWindow, canceled.ResumeWindowExpirationDate);
        Assert.Equal(_boundUserId, canceled.UserId);
        Assert.Equal(accountRef, canceled.AccountRef);
    }

    [Fact]
    public async Task TransitionAsync_ChangedSinceRead_ReturnsConflictAndLogsNothing()
    {
        var sutProvider = CreateSutProvider();
        var entitlement = CreateEntitlement(PartnershipEntitlementState.Active);
        var readRevisionDate = entitlement.RevisionDate;
        ArrangePartnership(sutProvider, entitlement);
        sutProvider.GetDependency<IOrganizationPartnershipEntitlementRepository>()
            .ReplaceIfUnchangedAsync(entitlement, readRevisionDate)
            .Returns(false);

        var result = await sutProvider.Sut.TransitionAsync(CreateRequest(entitlement, PartnershipEntitlementAction.Suspend));

        Assert.Equal("entitlement_conflict", Assert.IsType<EntitlementConcurrentlyModified>(result.AsError).Code);
        Assert.Empty(sutProvider.GetDependency<IEventService>().ReceivedCalls());
    }

    [Fact]
    public async Task TransitionAsync_UserExit_ReleasesBindingWithNoResumeWindow()
    {
        var sutProvider = CreateSutProvider();
        var entitlement = CreateEntitlement(PartnershipEntitlementState.Active);
        ArrangePartnership(sutProvider, entitlement);

        var result = await sutProvider.Sut.TransitionAsync(CreateRequest(entitlement, PartnershipEntitlementAction.UserExit));

        var exited = result.AsSuccess.Entitlement;
        Assert.Equal(PartnershipEntitlementState.Canceled, exited.State);
        Assert.Null(exited.UserId);
        Assert.Null(exited.AccountRef);
        Assert.Null(exited.ResumeWindowExpirationDate);
    }

    [Fact]
    public async Task TransitionAsync_PartnerResumeAfterUserExit_ReturnsNotResumable()
    {
        var sutProvider = CreateSutProvider();
        var entitlement = CreateEntitlement(PartnershipEntitlementState.Active);
        ArrangePartnership(sutProvider, entitlement);

        await sutProvider.Sut.TransitionAsync(CreateRequest(entitlement, PartnershipEntitlementAction.UserExit));
        var result = await sutProvider.Sut.TransitionAsync(CreateRequest(entitlement, PartnershipEntitlementAction.Resume));

        Assert.IsType<EntitlementNotResumable>(result.AsError);
        Assert.Null(entitlement.UserId);
    }

    [Fact]
    public async Task TransitionAsync_CancelThenResumeInsideWindow_KeepsSameUserAndAccountRef()
    {
        var sutProvider = CreateSutProvider();
        var entitlement = CreateEntitlement(PartnershipEntitlementState.Active);
        var accountRef = entitlement.AccountRef;
        ArrangePartnership(sutProvider, entitlement);

        await sutProvider.Sut.TransitionAsync(CreateRequest(entitlement, PartnershipEntitlementAction.Cancel));
        sutProvider.GetDependency<FakeTimeProvider>().Advance(_resumeWindow - TimeSpan.FromMinutes(1));
        var result = await sutProvider.Sut.TransitionAsync(CreateRequest(entitlement, PartnershipEntitlementAction.Resume));

        var resumed = result.AsSuccess.Entitlement;
        Assert.Equal(PartnershipEntitlementState.Active, resumed.State);
        Assert.Equal(_boundUserId, resumed.UserId);
        Assert.Equal(accountRef, resumed.AccountRef);
        Assert.Null(resumed.CanceledDate);
        Assert.Null(resumed.ResumeWindowExpirationDate);
    }

    [Fact]
    public async Task TransitionAsync_ResumePastWindow_ReturnsNotResumable()
    {
        var sutProvider = CreateSutProvider();
        var entitlement = CreateEntitlement(PartnershipEntitlementState.Active);
        ArrangePartnership(sutProvider, entitlement);

        await sutProvider.Sut.TransitionAsync(CreateRequest(entitlement, PartnershipEntitlementAction.Cancel));
        sutProvider.GetDependency<FakeTimeProvider>().Advance(_resumeWindow);
        var result = await sutProvider.Sut.TransitionAsync(CreateRequest(entitlement, PartnershipEntitlementAction.Resume));

        Assert.True(result.IsError);
        var error = Assert.IsType<EntitlementNotResumable>(result.AsError);
        Assert.Equal("entitlement_not_resumable", error.Code);
        Assert.Equal(PartnershipEntitlementReason.ResumeWindowExpired, error.Reason);
    }

    [Fact]
    public async Task TransitionAsync_ActivateByHeldUserPastWindow_LogsBindingFailed()
    {
        var sutProvider = CreateSutProvider();
        var entitlement = CreateEntitlement(PartnershipEntitlementState.Canceled);
        entitlement.ResumeWindowExpirationDate = _now;
        var partnership = ArrangePartnership(sutProvider, entitlement);

        var result = await sutProvider.Sut.TransitionAsync(CreateRequest(entitlement, PartnershipEntitlementAction.Activate));

        Assert.IsType<EntitlementNotResumable>(result.AsError);
        await sutProvider.GetDependency<IEventService>()
            .Received(1)
            .LogOrganizationPartnershipEventAsync(partnership.OrganizationId, EventType.PartnershipEntitlement_BindingFailed, _now);
    }

    [Fact]
    public async Task TransitionAsync_ActivateByDifferentUserWhileCanceledHeld_ReturnsAlreadyBound()
    {
        var sutProvider = CreateSutProvider();
        var entitlement = CreateEntitlement(PartnershipEntitlementState.Canceled);
        ArrangePartnership(sutProvider, entitlement);

        var result = await sutProvider.Sut.TransitionAsync(
            CreateRequest(entitlement, PartnershipEntitlementAction.Activate, userId: Guid.NewGuid()));

        var error = Assert.IsType<EntitlementAlreadyBound>(result.AsError);
        Assert.Equal(PartnershipEntitlementReason.AlreadyBound, error.Reason);
        Assert.Equal(_boundUserId, entitlement.UserId);
    }

    [Fact]
    public async Task TransitionAsync_ActivateByDifferentUserWhileCanceledHeld_LogsBindingFailed()
    {
        var sutProvider = CreateSutProvider();
        var entitlement = CreateEntitlement(PartnershipEntitlementState.Canceled);
        var partnership = ArrangePartnership(sutProvider, entitlement);

        await sutProvider.Sut.TransitionAsync(
            CreateRequest(entitlement, PartnershipEntitlementAction.Activate, userId: Guid.NewGuid()));

        await sutProvider.GetDependency<IEventService>()
            .Received(1)
            .LogOrganizationPartnershipEventAsync(partnership.OrganizationId, EventType.PartnershipEntitlement_BindingFailed, _now);
    }

    [Fact]
    public async Task TransitionAsync_ActivateUnknownExternalId_ReturnsNotProvisionedAndLogsBindingFailed()
    {
        var sutProvider = CreateSutProvider();
        var entitlement = CreateEntitlement(PartnershipEntitlementState.Provisioned);
        var partnership = ArrangePartnership(sutProvider, entitlement);
        var request = CreateRequest(entitlement, PartnershipEntitlementAction.Activate) with { ExternalId = "unknown" };

        var result = await sutProvider.Sut.TransitionAsync(request);

        Assert.Equal("not_provisioned", Assert.IsType<EntitlementNotProvisioned>(result.AsError).Code);
        await sutProvider.GetDependency<IEventService>()
            .Received(1)
            .LogOrganizationPartnershipEventAsync(partnership.OrganizationId, EventType.PartnershipEntitlement_BindingFailed, _now);
    }

    [Fact]
    public async Task TransitionAsync_SuspendUnknownExternalId_ReturnsNotFound()
    {
        var sutProvider = CreateSutProvider();
        var entitlement = CreateEntitlement(PartnershipEntitlementState.Active);
        ArrangePartnership(sutProvider, entitlement);
        var request = CreateRequest(entitlement, PartnershipEntitlementAction.Suspend) with { ExternalId = "unknown" };

        var result = await sutProvider.Sut.TransitionAsync(request);

        Assert.IsType<EntitlementNotFound>(result.AsError);
    }

    [Fact]
    public async Task TransitionAsync_StaleEffectiveAt_ReturnsCurrentEntitlementNotApplied()
    {
        var sutProvider = CreateSutProvider();
        var entitlement = CreateEntitlement(PartnershipEntitlementState.Active);
        ArrangePartnership(sutProvider, entitlement);
        var lastApplied = entitlement.LastAppliedEffectiveDate;

        var result = await sutProvider.Sut.TransitionAsync(
            CreateRequest(entitlement, PartnershipEntitlementAction.Suspend, effectiveAt: lastApplied.AddSeconds(-1)));

        Assert.True(result.IsSuccess);
        Assert.False(result.AsSuccess.Applied);
        Assert.Equal(PartnershipEntitlementAppliedReasons.StaleTransition, result.AsSuccess.AppliedReason);
        Assert.Equal(lastApplied, result.AsSuccess.LastAppliedAt);
        Assert.Equal(PartnershipEntitlementState.Active, result.AsSuccess.Entitlement.State);
    }

    [Fact]
    public async Task TransitionAsync_StaleEffectiveAt_DoesNotSaveOrLog()
    {
        var sutProvider = CreateSutProvider();
        var entitlement = CreateEntitlement(PartnershipEntitlementState.Active);
        ArrangePartnership(sutProvider, entitlement);

        await sutProvider.Sut.TransitionAsync(CreateRequest(entitlement, PartnershipEntitlementAction.Suspend,
            effectiveAt: entitlement.LastAppliedEffectiveDate.AddSeconds(-1)));

        await sutProvider.GetDependency<IOrganizationPartnershipEntitlementRepository>()
            .DidNotReceiveWithAnyArgs()
            .ReplaceIfUnchangedAsync(default!, default);
        Assert.Empty(sutProvider.GetDependency<IEventService>().ReceivedCalls());
    }

    [Fact]
    public async Task TransitionAsync_EffectiveAtEqualToLastApplied_IsApplied()
    {
        var sutProvider = CreateSutProvider();
        var entitlement = CreateEntitlement(PartnershipEntitlementState.Active);
        ArrangePartnership(sutProvider, entitlement);

        var result = await sutProvider.Sut.TransitionAsync(CreateRequest(entitlement, PartnershipEntitlementAction.Suspend,
            effectiveAt: entitlement.LastAppliedEffectiveDate));

        Assert.True(result.AsSuccess.Applied);
        Assert.Equal(PartnershipEntitlementState.Suspended, result.AsSuccess.Entitlement.State);
    }

    [Fact]
    public async Task TransitionAsync_ReplayedIdenticalRequest_IsEvaluatedAgainstNewStateAndRejected()
    {
        var sutProvider = CreateSutProvider();
        var entitlement = CreateEntitlement(PartnershipEntitlementState.Active);
        ArrangePartnership(sutProvider, entitlement);
        var request = CreateRequest(entitlement, PartnershipEntitlementAction.Suspend, effectiveAt: _now.AddMinutes(-5));

        await sutProvider.Sut.TransitionAsync(request);
        var replay = await sutProvider.Sut.TransitionAsync(request);

        Assert.IsType<IllegalEntitlementTransition>(replay.AsError);
        Assert.Equal(PartnershipEntitlementState.Suspended, entitlement.State);
    }

    [Fact]
    public async Task TransitionAsync_EffectiveAtInFuture_ReturnsError()
    {
        var sutProvider = CreateSutProvider();
        var entitlement = CreateEntitlement(PartnershipEntitlementState.Active);
        ArrangePartnership(sutProvider, entitlement);

        var result = await sutProvider.Sut.TransitionAsync(
            CreateRequest(entitlement, PartnershipEntitlementAction.Suspend, effectiveAt: _now.AddSeconds(1)));

        Assert.Equal("effective_at_in_future", Assert.IsType<EffectiveAtInFuture>(result.AsError).Code);
        Assert.Equal(PartnershipEntitlementState.Active, entitlement.State);
    }

    [Fact]
    public async Task TransitionAsync_PartnershipNotActive_ReturnsError()
    {
        var sutProvider = CreateSutProvider();
        var entitlement = CreateEntitlement(PartnershipEntitlementState.Active);
        ArrangePartnership(sutProvider, entitlement, PartnershipStatus.Inactive);

        var result = await sutProvider.Sut.TransitionAsync(CreateRequest(entitlement, PartnershipEntitlementAction.Suspend));

        Assert.IsType<PartnershipNotActive>(result.AsError);
    }

    [Fact]
    public async Task TransitionAsync_UserExitWhenPartnershipNotActive_IsApplied()
    {
        var sutProvider = CreateSutProvider();
        var entitlement = CreateEntitlement(PartnershipEntitlementState.Active);
        ArrangePartnership(sutProvider, entitlement, PartnershipStatus.Inactive);

        var result = await sutProvider.Sut.TransitionAsync(CreateRequest(entitlement, PartnershipEntitlementAction.UserExit));

        Assert.Equal(PartnershipEntitlementState.Canceled, result.AsSuccess.Entitlement.State);
    }

    [Fact]
    public async Task TransitionAsync_UserExitByUserNotBound_ReturnsNotFound()
    {
        var sutProvider = CreateSutProvider();
        var entitlement = CreateEntitlement(PartnershipEntitlementState.Active);
        ArrangePartnership(sutProvider, entitlement);

        var result = await sutProvider.Sut.TransitionAsync(
            CreateRequest(entitlement, PartnershipEntitlementAction.UserExit, userId: Guid.NewGuid()));

        Assert.IsType<EntitlementNotFound>(result.AsError);
    }

    [Theory]
    [InlineData(PartnershipEntitlementAction.Activate)]
    [InlineData(PartnershipEntitlementAction.UserExit)]
    public async Task TransitionAsync_UserActionWithoutUserId_ReturnsValidationError(PartnershipEntitlementAction action)
    {
        var sutProvider = CreateSutProvider();
        var entitlement = CreateEntitlement(PartnershipEntitlementState.Active);
        ArrangePartnership(sutProvider, entitlement);

        var result = await sutProvider.Sut.TransitionAsync(CreateRequest(entitlement, action) with { UserId = null });

        Assert.Equal("user_id_required", Assert.IsType<UserIdRequired>(result.AsError).Code);
    }

    [Theory]
    [InlineData(PartnershipEntitlementAction.Cancel, PartnershipEntitlementReason.UserExit)]
    [InlineData(PartnershipEntitlementAction.Suspend, PartnershipEntitlementReason.AlreadyBound)]
    [InlineData(PartnershipEntitlementAction.UserExit, PartnershipEntitlementReason.CustomerRequest)]
    [InlineData(PartnershipEntitlementAction.Activate, PartnershipEntitlementReason.CustomerRequest)]
    public async Task TransitionAsync_ReasonNotAllowedForAction_ReturnsValidationError(
        PartnershipEntitlementAction action, PartnershipEntitlementReason reason)
    {
        var sutProvider = CreateSutProvider();
        var entitlement = CreateEntitlement(PartnershipEntitlementState.Active);
        ArrangePartnership(sutProvider, entitlement);

        var result = await sutProvider.Sut.TransitionAsync(CreateRequest(entitlement, action) with { Reason = reason });

        Assert.Equal("invalid_reason", Assert.IsType<InvalidEntitlementReason>(result.AsError).Code);
    }

    [Theory]
    [InlineData(PartnershipEntitlementReason.BillingLapse)]
    [InlineData(PartnershipEntitlementReason.CustomerRequest)]
    [InlineData(PartnershipEntitlementReason.SubscriptionEnded)]
    [InlineData(PartnershipEntitlementReason.FraudHold)]
    [InlineData(PartnershipEntitlementReason.PlanChange)]
    [InlineData(PartnershipEntitlementReason.Administrative)]
    public async Task TransitionAsync_PartnerReason_IsAccepted(PartnershipEntitlementReason reason)
    {
        var sutProvider = CreateSutProvider();
        var entitlement = CreateEntitlement(PartnershipEntitlementState.Active);
        ArrangePartnership(sutProvider, entitlement);

        var result = await sutProvider.Sut.TransitionAsync(
            CreateRequest(entitlement, PartnershipEntitlementAction.Suspend) with { Reason = reason });

        Assert.True(result.IsSuccess);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task TransitionAsync_BlankExternalId_ReturnsValidationError(string externalId)
    {
        var sutProvider = CreateSutProvider();
        var entitlement = CreateEntitlement(PartnershipEntitlementState.Active);
        ArrangePartnership(sutProvider, entitlement);

        var result = await sutProvider.Sut.TransitionAsync(
            CreateRequest(entitlement, PartnershipEntitlementAction.Suspend) with { ExternalId = externalId });

        Assert.Equal("invalid_external_id", Assert.IsType<InvalidExternalId>(result.AsError).Code);
    }

    [Theory]
    [MemberData(nameof(LegalTransitions))]
    public async Task TransitionAsync_LegalTransition_LogsOnlyThroughPartnershipEventMethod(
        PartnershipEntitlementState from, PartnershipEntitlementAction action)
    {
        var sutProvider = CreateSutProvider();
        var entitlement = CreateEntitlement(from);
        ArrangePartnership(sutProvider, entitlement);

        await sutProvider.Sut.TransitionAsync(CreateRequest(entitlement, action));

        // The partnership method carries no user identity; every other IEventService method can.
        Assert.All(sutProvider.GetDependency<IEventService>().ReceivedCalls(), call =>
            Assert.Equal(nameof(IEventService.LogOrganizationPartnershipEventAsync), call.GetMethodInfo().Name));
    }

    private static SutProvider<TransitionPartnershipEntitlementCommand> CreateSutProvider()
    {
        var globalSettings = new GlobalSettings
        {
            Partnerships = new GlobalSettings.PartnershipSettings { ResumeWindow = _resumeWindow },
        };
        var sutProvider = new SutProvider<TransitionPartnershipEntitlementCommand>()
            .SetDependency(globalSettings)
            .WithFakeTimeProvider()
            .Create();
        sutProvider.GetDependency<FakeTimeProvider>().SetUtcNow(_now);
        sutProvider.GetDependency<IOrganizationPartnershipEntitlementRepository>()
            .ReplaceIfUnchangedAsync(Arg.Any<OrganizationPartnershipEntitlement>(), Arg.Any<DateTime>())
            .Returns(true);
        return sutProvider;
    }

    private static OrganizationPartnership ArrangePartnership(
        SutProvider<TransitionPartnershipEntitlementCommand> sutProvider,
        OrganizationPartnershipEntitlement entitlement,
        PartnershipStatus status = PartnershipStatus.Active)
    {
        var partnership = new OrganizationPartnership
        {
            Id = entitlement.OrganizationPartnershipId,
            OrganizationId = Guid.NewGuid(),
            Name = "Partner",
            Status = status,
        };
        sutProvider.GetDependency<IOrganizationPartnershipRepository>()
            .GetByIdAsync(partnership.Id)
            .Returns(partnership);
        sutProvider.GetDependency<IOrganizationPartnershipEntitlementRepository>()
            .GetByExternalIdAsync(partnership.Id, entitlement.ExternalId)
            .Returns(entitlement);
        return partnership;
    }

    private static OrganizationPartnershipEntitlement CreateEntitlement(PartnershipEntitlementState state)
    {
        var entitlement = new OrganizationPartnershipEntitlement
        {
            Id = Guid.NewGuid(),
            OrganizationPartnershipId = Guid.NewGuid(),
            ExternalId = "customer-1",
            ExternalIdHash = "hash",
            State = state,
            LastAppliedEffectiveDate = _now.AddDays(-1),
        };

        if (state != PartnershipEntitlementState.Provisioned)
        {
            entitlement.UserId = _boundUserId;
            entitlement.SetNewAccountRef();
            entitlement.BoundDate = _now.AddDays(-10);
        }

        if (state == PartnershipEntitlementState.Suspended)
        {
            entitlement.SuspendedDate = _now.AddDays(-1);
        }

        if (state == PartnershipEntitlementState.Canceled)
        {
            entitlement.CanceledDate = _now.AddDays(-1);
            entitlement.ResumeWindowExpirationDate = entitlement.CanceledDate + _resumeWindow;
        }

        return entitlement;
    }

    private static TransitionPartnershipEntitlementRequest CreateRequest(
        OrganizationPartnershipEntitlement entitlement,
        PartnershipEntitlementAction action,
        Guid? userId = null,
        DateTime? effectiveAt = null) =>
        new()
        {
            OrganizationPartnershipId = entitlement.OrganizationPartnershipId,
            ExternalId = entitlement.ExternalId,
            Action = action,
            UserId = action is PartnershipEntitlementAction.Activate or PartnershipEntitlementAction.UserExit
                ? userId ?? _boundUserId
                : null,
            EffectiveAt = effectiveAt,
        };
}
