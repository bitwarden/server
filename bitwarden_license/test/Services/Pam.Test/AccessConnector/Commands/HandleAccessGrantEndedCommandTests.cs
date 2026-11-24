using Bit.Core;
using Bit.Pam.Entities;
using Bit.Pam.Enums;
using Bit.Pam.Models;
using Bit.Pam.Repositories;
using Bit.Services.Pam.AccessConnector.Commands;
using Bit.Services.Pam.AccessConnector.Commands.Interfaces;
using Bit.Services.Pam.Services;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Bitwarden.Server.Sdk.Features;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Xunit;

namespace Bit.Services.Pam.Test.AccessConnector.Commands;

[SutProviderCustomize]
public class HandleAccessGrantEndedCommandTests
{
    private static readonly DateTime _now = new(2026, 6, 5, 12, 0, 0, DateTimeKind.Utc);

    [Theory, BitAutoData]
    public async Task HandleAsync_FlagOff_NoOp(Guid cipherId)
    {
        var sutProvider = Setup();
        sutProvider.GetDependency<IFeatureService>().IsEnabled(FeatureFlagKeys.PamAccessConnector).Returns(false);

        await sutProvider.Sut.HandleAsync(cipherId);

        await sutProvider.GetDependency<IPamRotationConfigRepository>().DidNotReceiveWithAnyArgs()
            .GetByCipherIdAsync(default);
        await sutProvider.GetDependency<IOfferRotationCommand>().DidNotReceiveWithAnyArgs()
            .OfferAsync(default, default);
        await sutProvider.GetDependency<IAccessAuditEventEmitter>().DidNotReceiveWithAnyArgs()
            .EmitAsync(default!);
    }

    [Theory, BitAutoData]
    public async Task HandleAsync_NoConfig_NoOp(Guid cipherId)
    {
        var sutProvider = Setup();
        sutProvider.GetDependency<IPamRotationConfigRepository>().GetByCipherIdAsync(cipherId)
            .Returns((PamRotationConfig?)null);

        await sutProvider.Sut.HandleAsync(cipherId);

        await sutProvider.GetDependency<IOfferRotationCommand>().DidNotReceiveWithAnyArgs()
            .OfferAsync(default, default);
        await sutProvider.GetDependency<IAccessAuditEventEmitter>().DidNotReceiveWithAnyArgs()
            .EmitAsync(default!);
    }

    [Theory, BitAutoData]
    public async Task HandleAsync_NotOptedIn_DropsPendingAccessEnd(PamRotationConfig config)
    {
        var sutProvider = Setup();
        config.RotateOnAccessEnd = false;
        sutProvider.GetDependency<IPamRotationConfigRepository>().GetByCipherIdAsync(config.CipherId)
            .Returns(config);

        await sutProvider.Sut.HandleAsync(config.CipherId);

        await sutProvider.GetDependency<IOfferRotationCommand>().DidNotReceiveWithAnyArgs()
            .OfferAsync(default, default);
        await sutProvider.GetDependency<IPamRotationConfigRepository>().Received(1)
            .DeletePendingAccessEndAsync(config.Id);
        await sutProvider.GetDependency<IAccessAuditEventEmitter>().DidNotReceiveWithAnyArgs()
            .EmitAsync(default!);
    }

    [Theory, BitAutoData]
    public async Task HandleAsync_Paused_DropsPendingAccessEnd(PamRotationConfig config)
    {
        var sutProvider = Setup();
        config.RotateOnAccessEnd = true;
        config.Enabled = false;
        sutProvider.GetDependency<IPamRotationConfigRepository>().GetByCipherIdAsync(config.CipherId)
            .Returns(config);

        await sutProvider.Sut.HandleAsync(config.CipherId);

        await sutProvider.GetDependency<IOfferRotationCommand>().DidNotReceiveWithAnyArgs()
            .OfferAsync(default, default);
        await sutProvider.GetDependency<IPamRotationConfigRepository>().DidNotReceiveWithAnyArgs()
            .RaiseManualObligationOnAccessEndAsync(default, default);
        await sutProvider.GetDependency<IPamRotationConfigRepository>().Received(1)
            .DeletePendingAccessEndAsync(config.Id);
        await sutProvider.GetDependency<IAccessAuditEventEmitter>().DidNotReceiveWithAnyArgs()
            .EmitAsync(default!);
    }

    [Theory, BitAutoData]
    public async Task HandleAsync_AutomaticOptedIn_OffersWithAccessEndSourceThenClearsPending(
        PamRotationConfig config, PamTargetSystem target)
    {
        var sutProvider = Setup();
        config.RotateOnAccessEnd = true;
        config.Enabled = true;
        target.Id = config.TargetSystemId;
        target.Method = PamTargetSystemMethod.Automatic;
        sutProvider.GetDependency<IPamRotationConfigRepository>().GetByCipherIdAsync(config.CipherId)
            .Returns(config);
        sutProvider.GetDependency<IPamTargetSystemRepository>().GetByIdAsync(config.TargetSystemId).Returns(target);

        await sutProvider.Sut.HandleAsync(config.CipherId);

        Received.InOrder(() =>
        {
            sutProvider.GetDependency<IOfferRotationCommand>().OfferAsync(config.Id, PamRotationSource.AccessEnd);
            sutProvider.GetDependency<IPamRotationConfigRepository>().DeletePendingAccessEndAsync(config.Id, _now);
        });
        await sutProvider.GetDependency<IPamRotationConfigRepository>().DidNotReceiveWithAnyArgs()
            .ReplaceAsync(default!);
        // OfferRotationCommand audits the offer itself.
        await sutProvider.GetDependency<IAccessAuditEventEmitter>().DidNotReceiveWithAnyArgs()
            .EmitAsync(default!);
    }

    // A lease that ends while the offer runs may not be covered by it, so its row must survive the clear.
    [Theory, BitAutoData]
    public async Task HandleAsync_AutomaticOptedIn_KeepsRowsRecordedDuringTheOffer(
        PamRotationConfig config, PamTargetSystem target)
    {
        var sutProvider = Setup();
        config.RotateOnAccessEnd = true;
        config.Enabled = true;
        target.Id = config.TargetSystemId;
        target.Method = PamTargetSystemMethod.Automatic;
        sutProvider.GetDependency<IPamRotationConfigRepository>().GetByCipherIdAsync(config.CipherId)
            .Returns(config);
        sutProvider.GetDependency<IPamTargetSystemRepository>().GetByIdAsync(config.TargetSystemId).Returns(target);
        sutProvider.GetDependency<IOfferRotationCommand>().OfferAsync(config.Id, PamRotationSource.AccessEnd)
            .Returns(_ =>
            {
                sutProvider.GetDependency<FakeTimeProvider>().Advance(TimeSpan.FromSeconds(5));
                return PamRotationJobCreateOutcome.Created;
            });

        await sutProvider.Sut.HandleAsync(config.CipherId);

        await sutProvider.GetDependency<IPamRotationConfigRepository>().Received(1)
            .DeletePendingAccessEndAsync(config.Id, _now);
    }

    // The pending access end must outlive a failed offer, so the rotation sweep can retry it.
    [Theory, BitAutoData]
    public async Task HandleAsync_AutomaticOfferThrows_KeepsPendingAccessEnd(
        PamRotationConfig config, PamTargetSystem target)
    {
        var sutProvider = Setup();
        config.RotateOnAccessEnd = true;
        config.Enabled = true;
        target.Id = config.TargetSystemId;
        target.Method = PamTargetSystemMethod.Automatic;
        sutProvider.GetDependency<IPamRotationConfigRepository>().GetByCipherIdAsync(config.CipherId)
            .Returns(config);
        sutProvider.GetDependency<IPamTargetSystemRepository>().GetByIdAsync(config.TargetSystemId).Returns(target);
        sutProvider.GetDependency<IOfferRotationCommand>().OfferAsync(config.Id, PamRotationSource.AccessEnd)
            .Returns(Task.FromException<PamRotationJobCreateOutcome>(new InvalidOperationException("boom")));

        await Assert.ThrowsAsync<InvalidOperationException>(() => sutProvider.Sut.HandleAsync(config.CipherId));

        await sutProvider.GetDependency<IPamRotationConfigRepository>().DidNotReceiveWithAnyArgs()
            .DeletePendingAccessEndAsync(default);
    }

    [Theory, BitAutoData]
    public async Task HandleAsync_ManualOptedInEnabled_RaisesObligationAndEmitsManualRotationDueAudit(
        PamRotationConfig config, PamTargetSystem target)
    {
        var sutProvider = Setup();
        config.RotateOnAccessEnd = true;
        config.Enabled = true;
        target.Id = config.TargetSystemId;
        target.Method = PamTargetSystemMethod.Manual;
        sutProvider.GetDependency<IPamRotationConfigRepository>().GetByCipherIdAsync(config.CipherId)
            .Returns(config);
        sutProvider.GetDependency<IPamTargetSystemRepository>().GetByIdAsync(config.TargetSystemId).Returns(target);
        sutProvider.GetDependency<IPamRotationConfigRepository>().RaiseManualObligationOnAccessEndAsync(config.Id, _now)
            .Returns(true);

        await sutProvider.Sut.HandleAsync(config.CipherId);

        await sutProvider.GetDependency<IOfferRotationCommand>().DidNotReceiveWithAnyArgs()
            .OfferAsync(default, default);
        await sutProvider.GetDependency<IPamRotationConfigRepository>().DidNotReceiveWithAnyArgs()
            .ReplaceAsync(default!);
        await sutProvider.GetDependency<IAccessAuditEventEmitter>().Received(1).EmitAsync(
            Arg.Is<AccessAuditEventData>(a => a.Kind == AccessAuditEventKind.ManualRotationDue
                && a.OrganizationId == config.OrganizationId
                && a.ActorId == null
                && a.CipherId == config.CipherId
                && a.RotationConfigId == config.Id
                && a.RotationSource == PamRotationSource.AccessEnd));
    }

    // A concurrent call (the inline trigger or a sweep retry) already raised it, so the audit is not repeated.
    [Theory, BitAutoData]
    public async Task HandleAsync_ManualNothingPending_EmitsNoAudit(PamRotationConfig config, PamTargetSystem target)
    {
        var sutProvider = Setup();
        config.RotateOnAccessEnd = true;
        config.Enabled = true;
        target.Id = config.TargetSystemId;
        target.Method = PamTargetSystemMethod.Manual;
        sutProvider.GetDependency<IPamRotationConfigRepository>().GetByCipherIdAsync(config.CipherId)
            .Returns(config);
        sutProvider.GetDependency<IPamTargetSystemRepository>().GetByIdAsync(config.TargetSystemId).Returns(target);
        sutProvider.GetDependency<IPamRotationConfigRepository>().RaiseManualObligationOnAccessEndAsync(config.Id, _now)
            .Returns(false);

        await sutProvider.Sut.HandleAsync(config.CipherId);

        await sutProvider.GetDependency<IAccessAuditEventEmitter>().DidNotReceiveWithAnyArgs()
            .EmitAsync(default!);
    }

    private static SutProvider<HandleAccessGrantEndedCommand> Setup()
    {
        var sutProvider = new SutProvider<HandleAccessGrantEndedCommand>().WithFakeTimeProvider().Create();
        sutProvider.GetDependency<FakeTimeProvider>().SetUtcNow(_now);
        sutProvider.GetDependency<IFeatureService>().IsEnabled(FeatureFlagKeys.PamAccessConnector).Returns(true);
        return sutProvider;
    }
}
