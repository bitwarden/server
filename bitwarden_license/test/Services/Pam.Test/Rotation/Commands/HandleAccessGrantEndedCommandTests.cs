using Bit.Core;
using Bit.Core.Services;
using Bit.Pam.Entities;
using Bit.Pam.Enums;
using Bit.Pam.Models;
using Bit.Pam.Repositories;
using Bit.Pam.Services;
using Bit.Services.Pam.Rotation.Commands;
using Bit.Services.Pam.Rotation.Commands.Interfaces;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Xunit;

namespace Bit.Services.Pam.Test.Rotation.Commands;

[SutProviderCustomize]
public class HandleAccessGrantEndedCommandTests
{
    private static readonly DateTime _now = new(2026, 6, 5, 12, 0, 0, DateTimeKind.Utc);

    [Theory, BitAutoData]
    public async Task HandleAsync_FlagOff_NoOp(Guid cipherId)
    {
        var sutProvider = Setup();
        sutProvider.GetDependency<IFeatureService>().IsEnabled(FeatureFlagKeys.PamRotation).Returns(false);

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
    public async Task HandleAsync_NotOptedIn_NoOp(PamRotationConfig config)
    {
        var sutProvider = Setup();
        config.RotateOnAccessEnd = false;
        sutProvider.GetDependency<IPamRotationConfigRepository>().GetByCipherIdAsync(config.CipherId)
            .Returns(config);

        await sutProvider.Sut.HandleAsync(config.CipherId);

        await sutProvider.GetDependency<IOfferRotationCommand>().DidNotReceiveWithAnyArgs()
            .OfferAsync(default, default);
        await sutProvider.GetDependency<IAccessAuditEventEmitter>().DidNotReceiveWithAnyArgs()
            .EmitAsync(default!);
    }

    [Theory, BitAutoData]
    public async Task HandleAsync_Paused_NoOp(PamRotationConfig config)
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
            .ReplaceAsync(default!);
        await sutProvider.GetDependency<IAccessAuditEventEmitter>().DidNotReceiveWithAnyArgs()
            .EmitAsync(default!);
    }

    [Theory, BitAutoData]
    public async Task HandleAsync_AutomaticOptedIn_OffersWithAccessEndSource(
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

        await sutProvider.GetDependency<IOfferRotationCommand>().Received(1)
            .OfferAsync(config.Id, PamRotationSource.AccessEnd);
        await sutProvider.GetDependency<IPamRotationConfigRepository>().DidNotReceiveWithAnyArgs()
            .ReplaceAsync(default!);
        // OfferRotationCommand owns its own audit; the automatic branch here emits nothing directly.
        await sutProvider.GetDependency<IAccessAuditEventEmitter>().DidNotReceiveWithAnyArgs()
            .EmitAsync(default!);
    }

    [Theory, BitAutoData]
    public async Task HandleAsync_ManualOptedInEnabled_SetsNextRotationAtNowAndEmitsManualRotationDueAudit(
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

        await sutProvider.Sut.HandleAsync(config.CipherId);

        await sutProvider.GetDependency<IOfferRotationCommand>().DidNotReceiveWithAnyArgs()
            .OfferAsync(default, default);
        await sutProvider.GetDependency<IPamRotationConfigRepository>().Received(1).ReplaceAsync(
            Arg.Is<PamRotationConfig>(c => c.Id == config.Id && c.NextRotationAt == _now));
        await sutProvider.GetDependency<IAccessAuditEventEmitter>().Received(1).EmitAsync(
            Arg.Is<AccessAuditEventData>(a => a.Kind == AccessAuditEventKind.ManualRotationDue
                && a.OrganizationId == config.OrganizationId
                && a.ActorId == null
                && a.CipherId == config.CipherId
                && a.RotationConfigId == config.Id
                && a.RotationSource == PamRotationSource.AccessEnd));
    }

    private static SutProvider<HandleAccessGrantEndedCommand> Setup()
    {
        var sutProvider = new SutProvider<HandleAccessGrantEndedCommand>().WithFakeTimeProvider().Create();
        sutProvider.GetDependency<FakeTimeProvider>().SetUtcNow(_now);
        sutProvider.GetDependency<IFeatureService>().IsEnabled(FeatureFlagKeys.PamRotation).Returns(true);
        return sutProvider;
    }
}
