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
public class UpdateTargetSystemCommandTests
{
    private static readonly DateTime _now = new(2026, 7, 6, 12, 0, 0, DateTimeKind.Utc);

    [Theory, BitAutoData]
    public async Task UpdateAsync_NameMissing_ThrowsBadRequest(
        Guid organizationId, Guid actingUserId, Guid targetSystemId, string sealedData)
    {
        var sutProvider = Setup();

        await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.UpdateAsync(organizationId, actingUserId, targetSystemId, " ", sealedData));

        await sutProvider.GetDependency<IPamTargetSystemRepository>().DidNotReceiveWithAnyArgs().ReplaceAsync(default!);
    }

    [Theory, BitAutoData]
    public async Task UpdateAsync_SealedDataMissing_ThrowsBadRequest(
        Guid organizationId, Guid actingUserId, Guid targetSystemId, string name)
    {
        var sutProvider = Setup();

        await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.UpdateAsync(organizationId, actingUserId, targetSystemId, name, " "));

        await sutProvider.GetDependency<IPamTargetSystemRepository>().DidNotReceiveWithAnyArgs().ReplaceAsync(default!);
    }

    [Theory, BitAutoData]
    public async Task UpdateAsync_TargetMissing_ThrowsNotFound(
        Guid organizationId, Guid actingUserId, Guid targetSystemId, string name, string sealedData)
    {
        var sutProvider = Setup();
        sutProvider.GetDependency<IPamTargetSystemRepository>().GetByIdAsync(targetSystemId).Returns((PamTargetSystem?)null);

        await Assert.ThrowsAsync<NotFoundException>(
            () => sutProvider.Sut.UpdateAsync(organizationId, actingUserId, targetSystemId, name, sealedData));

        await sutProvider.GetDependency<IPamTargetSystemRepository>().DidNotReceiveWithAnyArgs().ReplaceAsync(default!);
    }

    [Theory, BitAutoData]
    public async Task UpdateAsync_WrongOrg_ThrowsNotFound(
        Guid actingUserId, PamTargetSystem target, string name, string sealedData)
    {
        var sutProvider = Setup();
        sutProvider.GetDependency<IPamTargetSystemRepository>().GetByIdAsync(target.Id).Returns(target);

        await Assert.ThrowsAsync<NotFoundException>(
            () => sutProvider.Sut.UpdateAsync(Guid.NewGuid(), actingUserId, target.Id, name, sealedData));

        await sutProvider.GetDependency<IPamTargetSystemRepository>().DidNotReceiveWithAnyArgs().ReplaceAsync(default!);
    }

    [Theory, BitAutoData]
    public async Task UpdateAsync_PendingSeal_WritesBothFieldsAndMovesToActive(
        Guid actingUserId, PamTargetSystem target, string name, string sealedData)
    {
        var sutProvider = Setup();
        target.Status = PamTargetSystemStatus.PendingSeal;
        sutProvider.GetDependency<IPamTargetSystemRepository>().GetByIdAsync(target.Id).Returns(target);

        await sutProvider.Sut.UpdateAsync(target.OrganizationId, actingUserId, target.Id, name, sealedData);

        await sutProvider.GetDependency<IPamTargetSystemRepository>().Received(1).ReplaceAsync(Arg.Is<PamTargetSystem>(t =>
            t.Id == target.Id && t.Name == name && t.SealedData == sealedData
            && t.Status == PamTargetSystemStatus.Active && t.RevisionDate == _now));
    }

    [Theory, BitAutoData]
    public async Task UpdateAsync_Disabled_StaysDisabled(
        Guid actingUserId, PamTargetSystem target, string name, string sealedData)
    {
        var sutProvider = Setup();
        target.Status = PamTargetSystemStatus.Disabled;
        sutProvider.GetDependency<IPamTargetSystemRepository>().GetByIdAsync(target.Id).Returns(target);

        await sutProvider.Sut.UpdateAsync(target.OrganizationId, actingUserId, target.Id, name, sealedData);

        await sutProvider.GetDependency<IPamTargetSystemRepository>().Received(1).ReplaceAsync(Arg.Is<PamTargetSystem>(t =>
            t.Id == target.Id && t.SealedData == sealedData && t.Status == PamTargetSystemStatus.Disabled));
    }

    [Theory, BitAutoData]
    public async Task UpdateAsync_Active_StaysActive(
        Guid actingUserId, PamTargetSystem target, string name, string sealedData)
    {
        var sutProvider = Setup();
        target.Status = PamTargetSystemStatus.Active;
        sutProvider.GetDependency<IPamTargetSystemRepository>().GetByIdAsync(target.Id).Returns(target);

        await sutProvider.Sut.UpdateAsync(target.OrganizationId, actingUserId, target.Id, name, sealedData);

        await sutProvider.GetDependency<IPamTargetSystemRepository>().Received(1).ReplaceAsync(Arg.Is<PamTargetSystem>(t =>
            t.Id == target.Id && t.SealedData == sealedData && t.Status == PamTargetSystemStatus.Active));
    }

    [Theory, BitAutoData]
    public async Task UpdateAsync_NameChanged_EmitsAttemptThenOutcomeWithPriorNameInDetail(
        Guid actingUserId, PamTargetSystem target, string newName, string sealedData)
    {
        var sutProvider = Setup();
        var oldName = target.Name;
        sutProvider.GetDependency<IPamTargetSystemRepository>().GetByIdAsync(target.Id).Returns(target);

        await sutProvider.Sut.UpdateAsync(target.OrganizationId, actingUserId, target.Id, newName, sealedData);

        var emitter = sutProvider.GetDependency<IAccessAuditEventEmitter>();
        await emitter.Received(1).EmitAsync(Arg.Is<AccessAuditEventData>(e =>
            e.Kind == AccessAuditEventKind.TargetSystemUpdated && e.Phase == AccessAuditEventPhase.Attempt
            && e.TargetSystemId == target.Id && e.TargetSystemName == newName && e.Detail!.Contains(oldName)));
        await emitter.Received(1).EmitAsync(Arg.Is<AccessAuditEventData>(e =>
            e.Kind == AccessAuditEventKind.TargetSystemUpdated && e.Phase == AccessAuditEventPhase.Outcome
            && e.TargetSystemId == target.Id));
    }

    [Theory, BitAutoData]
    public async Task UpdateAsync_NameUnchanged_EmitsNoRenameDetail(
        Guid actingUserId, PamTargetSystem target, string sealedData)
    {
        var sutProvider = Setup();
        sutProvider.GetDependency<IPamTargetSystemRepository>().GetByIdAsync(target.Id).Returns(target);

        await sutProvider.Sut.UpdateAsync(target.OrganizationId, actingUserId, target.Id, target.Name, sealedData);

        var emitter = sutProvider.GetDependency<IAccessAuditEventEmitter>();
        await emitter.Received(2).EmitAsync(Arg.Is<AccessAuditEventData>(e =>
            e.Kind == AccessAuditEventKind.TargetSystemUpdated && e.Detail == null));
    }

    private static SutProvider<UpdateTargetSystemCommand> Setup()
    {
        var sutProvider = new SutProvider<UpdateTargetSystemCommand>().WithFakeTimeProvider().Create();
        sutProvider.GetDependency<FakeTimeProvider>().SetUtcNow(_now);
        return sutProvider;
    }
}
