using Bit.Pam.Entities;
using Bit.Pam.Enums;
using Bit.Pam.Models;
using Bit.Pam.Repositories;
using Bit.Pam.Services;
using Bit.Services.Pam.Rotation;
using Bit.Services.Pam.Rotation.Commands;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Xunit;

namespace Bit.Services.Pam.Test.Rotation.Commands;

[SutProviderCustomize]
public class OfferRotationCommandTests
{
    private static readonly DateTime _now = new(2026, 6, 5, 12, 0, 0, DateTimeKind.Utc);

    [Theory, BitAutoData]
    public async Task OfferAsync_ConfigMissing_ReturnsConfigNotOfferable_NoCreateGuardedCallNoAudit(
        Guid configId, PamRotationSource source)
    {
        var sutProvider = Setup();
        sutProvider.GetDependency<IPamRotationConfigRepository>().GetByIdAsync(configId)
            .Returns((PamRotationConfig?)null);

        var outcome = await sutProvider.Sut.OfferAsync(configId, source);

        Assert.Equal(PamRotationJobCreateOutcome.ConfigNotOfferable, outcome);
        await sutProvider.GetDependency<IPamRotationJobRepository>().DidNotReceiveWithAnyArgs()
            .CreateGuardedAsync(default!);
        await sutProvider.GetDependency<IAccessAuditEventEmitter>().DidNotReceiveWithAnyArgs()
            .EmitAsync(default!);
    }

    [Theory, BitAutoData]
    public async Task OfferAsync_TargetMissing_ReturnsConfigNotOfferable_NoCreateGuardedCallNoAudit(
        PamRotationConfig config, PamRotationSource source)
    {
        var sutProvider = Setup();
        SetupConfig(sutProvider, config);
        sutProvider.GetDependency<IPamTargetSystemRepository>().GetByIdAsync(config.TargetSystemId)
            .Returns((PamTargetSystem?)null);

        var outcome = await sutProvider.Sut.OfferAsync(config.Id, source);

        Assert.Equal(PamRotationJobCreateOutcome.ConfigNotOfferable, outcome);
        await sutProvider.GetDependency<IPamRotationJobRepository>().DidNotReceiveWithAnyArgs()
            .CreateGuardedAsync(default!);
        await sutProvider.GetDependency<IAccessAuditEventEmitter>().DidNotReceiveWithAnyArgs()
            .EmitAsync(default!);
    }

    [Theory, BitAutoData]
    public async Task OfferAsync_ConfigDisabled_ReturnsConfigNotOfferable_NoCreateGuardedCallNoAudit(
        PamRotationConfig config, PamTargetSystem target, PamRotationSource source)
    {
        var sutProvider = Setup();
        config.Enabled = false;
        target.Method = PamTargetSystemMethod.Automatic;
        target.Status = PamTargetSystemStatus.Active;
        SetupConfig(sutProvider, config);
        SetupTarget(sutProvider, config, target);

        var outcome = await sutProvider.Sut.OfferAsync(config.Id, source);

        Assert.Equal(PamRotationJobCreateOutcome.ConfigNotOfferable, outcome);
        await sutProvider.GetDependency<IPamRotationJobRepository>().DidNotReceiveWithAnyArgs()
            .CreateGuardedAsync(default!);
        await sutProvider.GetDependency<IAccessAuditEventEmitter>().DidNotReceiveWithAnyArgs()
            .EmitAsync(default!);
    }

    [Theory, BitAutoData]
    public async Task OfferAsync_TargetManual_ReturnsConfigNotOfferable_NoCreateGuardedCallNoAudit(
        PamRotationConfig config, PamTargetSystem target, PamRotationSource source)
    {
        var sutProvider = Setup();
        config.Enabled = true;
        target.Method = PamTargetSystemMethod.Manual;
        target.Status = PamTargetSystemStatus.Active;
        SetupConfig(sutProvider, config);
        SetupTarget(sutProvider, config, target);

        var outcome = await sutProvider.Sut.OfferAsync(config.Id, source);

        Assert.Equal(PamRotationJobCreateOutcome.ConfigNotOfferable, outcome);
        await sutProvider.GetDependency<IPamRotationJobRepository>().DidNotReceiveWithAnyArgs()
            .CreateGuardedAsync(default!);
        await sutProvider.GetDependency<IAccessAuditEventEmitter>().DidNotReceiveWithAnyArgs()
            .EmitAsync(default!);
    }

    [Theory, BitAutoData]
    public async Task OfferAsync_TargetInactive_ReturnsConfigNotOfferable_NoCreateGuardedCallNoAudit(
        PamRotationConfig config, PamTargetSystem target, PamRotationSource source)
    {
        var sutProvider = Setup();
        config.Enabled = true;
        target.Method = PamTargetSystemMethod.Automatic;
        target.Status = PamTargetSystemStatus.Disabled;
        SetupConfig(sutProvider, config);
        SetupTarget(sutProvider, config, target);

        var outcome = await sutProvider.Sut.OfferAsync(config.Id, source);

        Assert.Equal(PamRotationJobCreateOutcome.ConfigNotOfferable, outcome);
        await sutProvider.GetDependency<IPamRotationJobRepository>().DidNotReceiveWithAnyArgs()
            .CreateGuardedAsync(default!);
        await sutProvider.GetDependency<IAccessAuditEventEmitter>().DidNotReceiveWithAnyArgs()
            .EmitAsync(default!);
    }

    [Theory, BitAutoData]
    public async Task OfferAsync_Created_EmitsRotationOfferedAuditWithSource(
        PamRotationConfig config, PamTargetSystem target, PamRotationSource source)
    {
        var sutProvider = Setup();
        config.Enabled = true;
        target.Method = PamTargetSystemMethod.Automatic;
        target.Status = PamTargetSystemStatus.Active;
        SetupConfig(sutProvider, config);
        SetupTarget(sutProvider, config, target);
        sutProvider.GetDependency<IPamRotationJobRepository>()
            .CreateGuardedAsync(Arg.Any<PamRotationJob>())
            .Returns(PamRotationJobCreateOutcome.Created);

        var outcome = await sutProvider.Sut.OfferAsync(config.Id, source);

        Assert.Equal(PamRotationJobCreateOutcome.Created, outcome);
        await sutProvider.GetDependency<IPamRotationJobRepository>().Received(1).CreateGuardedAsync(
            Arg.Is<PamRotationJob>(j => j.RotationConfigId == config.Id
                && j.Source == source
                && j.Status == PamRotationJobStatus.Pending
                && j.ClaimedByDaemonId == null
                && j.ClaimedAt == null));
        await sutProvider.GetDependency<IAccessAuditEventEmitter>().Received(1).EmitAsync(
            Arg.Is<AccessAuditEventData>(a => a.Kind == AccessAuditEventKind.RotationOffered
                && a.OrganizationId == config.OrganizationId
                && a.ActorId == null
                && a.CipherId == config.CipherId
                && a.RotationConfigId == config.Id
                && a.TargetSystemId == target.Id
                && a.TargetSystemName == target.Name
                && a.RotationSource == source));
    }

    [Theory, BitAutoData]
    public async Task OfferAsync_ActiveJobExists_ReturnsSilentlyNoAudit(
        PamRotationConfig config, PamTargetSystem target, PamRotationSource source)
    {
        var sutProvider = Setup();
        config.Enabled = true;
        target.Method = PamTargetSystemMethod.Automatic;
        target.Status = PamTargetSystemStatus.Active;
        SetupConfig(sutProvider, config);
        SetupTarget(sutProvider, config, target);
        sutProvider.GetDependency<IPamRotationJobRepository>()
            .CreateGuardedAsync(Arg.Any<PamRotationJob>())
            .Returns(PamRotationJobCreateOutcome.ActiveJobExists);

        var outcome = await sutProvider.Sut.OfferAsync(config.Id, source);

        Assert.Equal(PamRotationJobCreateOutcome.ActiveJobExists, outcome);
        await sutProvider.GetDependency<IAccessAuditEventEmitter>().DidNotReceiveWithAnyArgs()
            .EmitAsync(default!);
    }

    private static SutProvider<OfferRotationCommand> Setup()
    {
        var sutProvider = new SutProvider<OfferRotationCommand>().WithFakeTimeProvider().Create();
        sutProvider.GetDependency<FakeTimeProvider>().SetUtcNow(_now);
        sutProvider.GetDependency<IOptions<PamRotationOptions>>().Value.Returns(new PamRotationOptions());
        return sutProvider;
    }

    private static void SetupConfig(SutProvider<OfferRotationCommand> sutProvider, PamRotationConfig config) =>
        sutProvider.GetDependency<IPamRotationConfigRepository>().GetByIdAsync(config.Id).Returns(config);

    private static void SetupTarget(SutProvider<OfferRotationCommand> sutProvider, PamRotationConfig config,
        PamTargetSystem target)
    {
        target.Id = config.TargetSystemId;
        sutProvider.GetDependency<IPamTargetSystemRepository>().GetByIdAsync(config.TargetSystemId).Returns(target);
    }
}
