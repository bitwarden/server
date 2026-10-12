using Bit.Core.Exceptions;
using Bit.Pam.Entities;
using Bit.Pam.Enums;
using Bit.Pam.Models;
using Bit.Pam.Repositories;
using Bit.Services.Pam.AccessConnector.Commands;
using Bit.Services.Pam.Services;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Xunit;

namespace Bit.Services.Pam.Test.AccessConnector.Commands;

[SutProviderCustomize]
public class SealTargetSystemCommandTests
{
    private static readonly DateTime _now = new(2026, 7, 6, 12, 0, 0, DateTimeKind.Utc);

    [Theory, BitAutoData]
    public async Task SealAsync_SealedDataMissing_ThrowsBadRequest(
        Guid organizationId, Guid actingUserId, Guid targetSystemId)
    {
        var sutProvider = Setup();

        await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.SealAsync(organizationId, actingUserId, targetSystemId, " "));

        await sutProvider.GetDependency<IPamTargetSystemRepository>().DidNotReceiveWithAnyArgs().ReplaceAsync(default!);
    }

    [Theory, BitAutoData]
    public async Task SealAsync_TargetMissing_ThrowsNotFound(
        Guid organizationId, Guid actingUserId, Guid targetSystemId, string sealedData)
    {
        var sutProvider = Setup();
        sutProvider.GetDependency<IPamTargetSystemRepository>().GetByIdAsync(targetSystemId).Returns((PamTargetSystem?)null);

        await Assert.ThrowsAsync<NotFoundException>(
            () => sutProvider.Sut.SealAsync(organizationId, actingUserId, targetSystemId, sealedData));

        await sutProvider.GetDependency<IPamTargetSystemRepository>().DidNotReceiveWithAnyArgs().ReplaceAsync(default!);
    }

    [Theory, BitAutoData]
    public async Task SealAsync_WrongOrg_ThrowsNotFound(Guid actingUserId, PamTargetSystem target, string sealedData)
    {
        var sutProvider = Setup();
        sutProvider.GetDependency<IPamTargetSystemRepository>().GetByIdAsync(target.Id).Returns(target);

        await Assert.ThrowsAsync<NotFoundException>(
            () => sutProvider.Sut.SealAsync(Guid.NewGuid(), actingUserId, target.Id, sealedData));

        await sutProvider.GetDependency<IPamTargetSystemRepository>().DidNotReceiveWithAnyArgs().ReplaceAsync(default!);
    }

    [Theory, BitAutoData]
    public async Task SealAsync_PendingSeal_MovesToActive(Guid actingUserId, PamTargetSystem target, string sealedData)
    {
        var sutProvider = Setup();
        target.Status = PamTargetSystemStatus.PendingSeal;
        sutProvider.GetDependency<IPamTargetSystemRepository>().GetByIdAsync(target.Id).Returns(target);

        await sutProvider.Sut.SealAsync(target.OrganizationId, actingUserId, target.Id, sealedData);

        await sutProvider.GetDependency<IPamTargetSystemRepository>().Received(1).ReplaceAsync(Arg.Is<PamTargetSystem>(t =>
            t.Id == target.Id && t.SealedData == sealedData
            && t.Status == PamTargetSystemStatus.Active && t.RevisionDate == _now));
    }

    [Theory, BitAutoData]
    public async Task SealAsync_Disabled_StaysDisabled(Guid actingUserId, PamTargetSystem target, string sealedData)
    {
        var sutProvider = Setup();
        target.Status = PamTargetSystemStatus.Disabled;
        sutProvider.GetDependency<IPamTargetSystemRepository>().GetByIdAsync(target.Id).Returns(target);

        await sutProvider.Sut.SealAsync(target.OrganizationId, actingUserId, target.Id, sealedData);

        await sutProvider.GetDependency<IPamTargetSystemRepository>().Received(1).ReplaceAsync(Arg.Is<PamTargetSystem>(t =>
            t.Id == target.Id && t.SealedData == sealedData && t.Status == PamTargetSystemStatus.Disabled));
    }

    [Theory, BitAutoData]
    public async Task SealAsync_Active_ReplacesSealedDataAndStaysActive(
        Guid actingUserId, PamTargetSystem target, string sealedData)
    {
        var sutProvider = Setup();
        target.Status = PamTargetSystemStatus.Active;
        sutProvider.GetDependency<IPamTargetSystemRepository>().GetByIdAsync(target.Id).Returns(target);

        await sutProvider.Sut.SealAsync(target.OrganizationId, actingUserId, target.Id, sealedData);

        await sutProvider.GetDependency<IPamTargetSystemRepository>().Received(1).ReplaceAsync(Arg.Is<PamTargetSystem>(t =>
            t.Id == target.Id && t.SealedData == sealedData && t.Status == PamTargetSystemStatus.Active));
    }

    [Theory, BitAutoData]
    public async Task SealAsync_HappyPath_EmitsAttemptThenOutcome(
        Guid actingUserId, PamTargetSystem target, string sealedData)
    {
        var sutProvider = Setup();
        target.Status = PamTargetSystemStatus.PendingSeal;
        sutProvider.GetDependency<IPamTargetSystemRepository>().GetByIdAsync(target.Id).Returns(target);

        await sutProvider.Sut.SealAsync(target.OrganizationId, actingUserId, target.Id, sealedData);

        var emitter = sutProvider.GetDependency<IAccessAuditEventEmitter>();
        await emitter.Received(1).EmitAsync(Arg.Is<AccessAuditEventData>(e =>
            e.Kind == AccessAuditEventKind.TargetSystemPolicyUpdated && e.Phase == AccessAuditEventPhase.Attempt
            && e.TargetSystemId == target.Id));
        await emitter.Received(1).EmitAsync(Arg.Is<AccessAuditEventData>(e =>
            e.Kind == AccessAuditEventKind.TargetSystemPolicyUpdated && e.Phase == AccessAuditEventPhase.Outcome
            && e.TargetSystemId == target.Id));
    }

    private static SutProvider<SealTargetSystemCommand> Setup()
    {
        var sutProvider = new SutProvider<SealTargetSystemCommand>().WithFakeTimeProvider().Create();
        sutProvider.GetDependency<FakeTimeProvider>().SetUtcNow(_now);
        return sutProvider;
    }
}
