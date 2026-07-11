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
public class RenameTargetSystemCommandTests
{
    private static readonly DateTime _now = new(2026, 7, 6, 12, 0, 0, DateTimeKind.Utc);

    [Theory, BitAutoData]
    public async Task RenameAsync_NameMissing_ThrowsBadRequest(Guid organizationId, Guid actingUserId, Guid targetSystemId)
    {
        var sutProvider = Setup();

        await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.RenameAsync(organizationId, actingUserId, targetSystemId, " "));

        await sutProvider.GetDependency<IPamTargetSystemRepository>().DidNotReceiveWithAnyArgs().ReplaceAsync(default!);
    }

    [Theory, BitAutoData]
    public async Task RenameAsync_TargetMissing_ThrowsNotFound(Guid organizationId, Guid actingUserId, Guid targetSystemId, string name)
    {
        var sutProvider = Setup();
        sutProvider.GetDependency<IPamTargetSystemRepository>().GetByIdAsync(targetSystemId).Returns((PamTargetSystem?)null);

        await Assert.ThrowsAsync<NotFoundException>(
            () => sutProvider.Sut.RenameAsync(organizationId, actingUserId, targetSystemId, name));

        await sutProvider.GetDependency<IPamTargetSystemRepository>().DidNotReceiveWithAnyArgs().ReplaceAsync(default!);
    }

    [Theory, BitAutoData]
    public async Task RenameAsync_WrongOrg_ThrowsNotFound(Guid actingUserId, PamTargetSystem target, string name)
    {
        var sutProvider = Setup();
        sutProvider.GetDependency<IPamTargetSystemRepository>().GetByIdAsync(target.Id).Returns(target);

        await Assert.ThrowsAsync<NotFoundException>(
            () => sutProvider.Sut.RenameAsync(Guid.NewGuid(), actingUserId, target.Id, name));

        await sutProvider.GetDependency<IPamTargetSystemRepository>().DidNotReceiveWithAnyArgs().ReplaceAsync(default!);
    }

    [Theory, BitAutoData]
    public async Task RenameAsync_HappyPath_UpdatesName(Guid actingUserId, PamTargetSystem target, string newName)
    {
        var sutProvider = Setup();
        var oldName = target.Name;
        sutProvider.GetDependency<IPamTargetSystemRepository>().GetByIdAsync(target.Id).Returns(target);

        await sutProvider.Sut.RenameAsync(target.OrganizationId, actingUserId, target.Id, newName);

        await sutProvider.GetDependency<IPamTargetSystemRepository>().Received(1).ReplaceAsync(Arg.Is<PamTargetSystem>(t =>
            t.Id == target.Id && t.Name == newName && t.RevisionDate == _now));
        Assert.NotEqual(oldName, newName);
    }

    [Theory, BitAutoData]
    public async Task RenameAsync_HappyPath_EmitsAttemptThenOutcomeWithPriorNameInDetail(
        Guid actingUserId, PamTargetSystem target, string newName)
    {
        var sutProvider = Setup();
        var oldName = target.Name;
        sutProvider.GetDependency<IPamTargetSystemRepository>().GetByIdAsync(target.Id).Returns(target);

        await sutProvider.Sut.RenameAsync(target.OrganizationId, actingUserId, target.Id, newName);

        var emitter = sutProvider.GetDependency<IAccessAuditEventEmitter>();
        await emitter.Received(1).EmitAsync(Arg.Is<AccessAuditEventData>(e =>
            e.Kind == AccessAuditEventKind.TargetSystemRenamed && e.Phase == AccessAuditEventPhase.Attempt
            && e.TargetSystemId == target.Id && e.TargetSystemName == newName && e.Detail!.Contains(oldName)));
        await emitter.Received(1).EmitAsync(Arg.Is<AccessAuditEventData>(e =>
            e.Kind == AccessAuditEventKind.TargetSystemRenamed && e.Phase == AccessAuditEventPhase.Outcome
            && e.TargetSystemId == target.Id));
    }

    private static SutProvider<RenameTargetSystemCommand> Setup()
    {
        var sutProvider = new SutProvider<RenameTargetSystemCommand>().WithFakeTimeProvider().Create();
        sutProvider.GetDependency<FakeTimeProvider>().SetUtcNow(_now);
        return sutProvider;
    }
}
