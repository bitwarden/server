using Bit.Core.Exceptions;
using Bit.Pam.Entities;
using Bit.Pam.Enums;
using Bit.Pam.Models;
using Bit.Pam.Repositories;
using Bit.Pam.Services;
using Bit.Services.Pam.Rotation.Commands;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Xunit;

namespace Bit.Services.Pam.Test.Rotation.Commands;

[SutProviderCustomize]
public class PauseRotationCommandTests
{
    private static readonly DateTime _now = new(2026, 7, 6, 12, 0, 0, DateTimeKind.Utc);

    [Theory, BitAutoData]
    public async Task PauseAsync_ConfigMissing_ThrowsNotFound(Guid organizationId, Guid actingUserId, Guid configId)
    {
        var sutProvider = Setup();
        sutProvider.GetDependency<IPamRotationConfigRepository>().GetByIdAsync(configId).Returns((PamRotationConfig?)null);

        await Assert.ThrowsAsync<NotFoundException>(
            () => sutProvider.Sut.PauseAsync(organizationId, actingUserId, configId));

        await sutProvider.GetDependency<IPamRotationConfigRepository>().DidNotReceiveWithAnyArgs().ReplaceAsync(default!);
    }

    [Theory, BitAutoData]
    public async Task PauseAsync_WrongOrg_ThrowsNotFound(Guid actingUserId, PamRotationConfig config)
    {
        var sutProvider = Setup();
        sutProvider.GetDependency<IPamRotationConfigRepository>().GetByIdAsync(config.Id).Returns(config);

        await Assert.ThrowsAsync<NotFoundException>(
            () => sutProvider.Sut.PauseAsync(Guid.NewGuid(), actingUserId, config.Id));

        await sutProvider.GetDependency<IPamRotationConfigRepository>().DidNotReceiveWithAnyArgs().ReplaceAsync(default!);
    }

    [Theory, BitAutoData]
    public async Task PauseAsync_AlreadyPaused_ThrowsBadRequest(Guid actingUserId, PamRotationConfig config)
    {
        var sutProvider = Setup();
        config.Enabled = false;
        sutProvider.GetDependency<IPamRotationConfigRepository>().GetByIdAsync(config.Id).Returns(config);

        await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.PauseAsync(config.OrganizationId, actingUserId, config.Id));

        await sutProvider.GetDependency<IPamRotationConfigRepository>().DidNotReceiveWithAnyArgs().ReplaceAsync(default!);
    }

    [Theory, BitAutoData]
    public async Task PauseAsync_Enabled_DisablesConfig(Guid actingUserId, PamRotationConfig config)
    {
        var sutProvider = Setup();
        config.Enabled = true;
        sutProvider.GetDependency<IPamRotationConfigRepository>().GetByIdAsync(config.Id).Returns(config);

        await sutProvider.Sut.PauseAsync(config.OrganizationId, actingUserId, config.Id);

        await sutProvider.GetDependency<IPamRotationConfigRepository>().Received(1).ReplaceAsync(Arg.Is<PamRotationConfig>(c =>
            c.Id == config.Id && !c.Enabled && c.RevisionDate == _now));
    }

    [Theory, BitAutoData]
    public async Task PauseAsync_Enabled_EmitsAttemptThenOutcome(Guid actingUserId, PamRotationConfig config)
    {
        var sutProvider = Setup();
        config.Enabled = true;
        sutProvider.GetDependency<IPamRotationConfigRepository>().GetByIdAsync(config.Id).Returns(config);

        await sutProvider.Sut.PauseAsync(config.OrganizationId, actingUserId, config.Id);

        var emitter = sutProvider.GetDependency<IAccessAuditEventEmitter>();
        await emitter.Received(1).EmitAsync(Arg.Is<AccessAuditEventData>(e =>
            e.Kind == AccessAuditEventKind.RotationPaused && e.Phase == AccessAuditEventPhase.Attempt
            && e.RotationConfigId == config.Id));
        await emitter.Received(1).EmitAsync(Arg.Is<AccessAuditEventData>(e =>
            e.Kind == AccessAuditEventKind.RotationPaused && e.Phase == AccessAuditEventPhase.Outcome
            && e.RotationConfigId == config.Id));
    }

    private static SutProvider<PauseRotationCommand> Setup()
    {
        var sutProvider = new SutProvider<PauseRotationCommand>().WithFakeTimeProvider().Create();
        sutProvider.GetDependency<FakeTimeProvider>().SetUtcNow(_now);
        return sutProvider;
    }
}
