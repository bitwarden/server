using Bit.Core.Exceptions;
using Bit.Pam.Entities;
using Bit.Pam.Enums;
using Bit.Pam.Models;
using Bit.Pam.Repositories;
using Bit.Services.Pam.OrganizationFeatures.Commands;
using Bit.Services.Pam.Services;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Xunit;

namespace Bit.Services.Pam.Test.Commands;

[SutProviderCustomize]
public class DeleteAccessRuleCommandTests
{
    [Theory, BitAutoData]
    public async Task DeleteAsync_HappyPath_HardDeletes(
        AccessRule existing, SutProvider<DeleteAccessRuleCommand> sutProvider)
    {
        sutProvider.GetDependency<IAccessRuleRepository>()
            .GetByIdAsync(existing.Id)
            .Returns(existing);

        await sutProvider.Sut.DeleteAsync(existing.OrganizationId, existing.Id, Guid.NewGuid());

        await sutProvider.GetDependency<IAccessRuleRepository>().Received(1)
            .DeleteAsync(existing);
    }

    [Theory, BitAutoData]
    public async Task DeleteAsync_MissingExisting_ThrowsNotFound(
        SutProvider<DeleteAccessRuleCommand> sutProvider)
    {
        sutProvider.GetDependency<IAccessRuleRepository>()
            .GetByIdAsync(Arg.Any<Guid>())
            .Returns((AccessRule?)null);

        await Assert.ThrowsAsync<NotFoundException>(
            () => sutProvider.Sut.DeleteAsync(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()));
        await sutProvider.GetDependency<IAccessRuleRepository>()
            .DidNotReceiveWithAnyArgs().DeleteAsync(default!);
    }

    [Theory, BitAutoData]
    public async Task DeleteAsync_WrongOrg_ThrowsNotFound(
        AccessRule existing, SutProvider<DeleteAccessRuleCommand> sutProvider)
    {
        sutProvider.GetDependency<IAccessRuleRepository>()
            .GetByIdAsync(existing.Id)
            .Returns(existing);

        await Assert.ThrowsAsync<NotFoundException>(
            () => sutProvider.Sut.DeleteAsync(Guid.NewGuid(), existing.Id, Guid.NewGuid()));
        await sutProvider.GetDependency<IAccessRuleRepository>()
            .DidNotReceiveWithAnyArgs().DeleteAsync(default!);
        await sutProvider.GetDependency<IAccessAuditEventEmitter>()
            .DidNotReceiveWithAnyArgs().EmitAsync(default!);
    }

    // The delete is hard, so the name has to be captured from the row before it goes -- nothing can resolve it after.
    [Theory, BitAutoData]
    public async Task DeleteAsync_EmitsAttemptThenOutcome_CarryingTheNameAndActor(AccessRule existing, Guid actorId)
    {
        var sutProvider = SetupSutProvider();
        existing.Name = "Production database";
        sutProvider.GetDependency<IAccessRuleRepository>().GetByIdAsync(existing.Id).Returns(existing);

        await sutProvider.Sut.DeleteAsync(existing.OrganizationId, existing.Id, actorId);

        var emitter = sutProvider.GetDependency<IAccessAuditEventEmitter>();
        await emitter.Received(1).EmitAsync(Arg.Is<AccessAuditEventData>(e =>
            e.Kind == AccessAuditEventKind.RuleDeleted && e.Phase == AccessAuditEventPhase.Attempt
            && e.OrganizationId == existing.OrganizationId && e.ActorId == actorId
            && e.AccessRuleId == existing.Id && e.RuleName == "Production database"
            && e.OccurredAt == _now));
        await emitter.Received(1).EmitAsync(Arg.Is<AccessAuditEventData>(e =>
            e.Kind == AccessAuditEventKind.RuleDeleted && e.Phase == AccessAuditEventPhase.Outcome
            && e.RuleName == "Production database"));
    }

    // An unresolvable caller is recorded as a system action rather than costing the event.
    [Theory, BitAutoData]
    public async Task DeleteAsync_NoResolvableCaller_RecordsASystemActor(AccessRule existing)
    {
        var sutProvider = SetupSutProvider();
        sutProvider.GetDependency<IAccessRuleRepository>().GetByIdAsync(existing.Id).Returns(existing);

        await sutProvider.Sut.DeleteAsync(existing.OrganizationId, existing.Id, null);

        await sutProvider.GetDependency<IAccessAuditEventEmitter>().Received(2)
            .EmitAsync(Arg.Is<AccessAuditEventData>(e => e.ActorId == null));
    }

    private static readonly DateTime _now = new(2026, 5, 21, 12, 0, 0, DateTimeKind.Utc);

    private static SutProvider<DeleteAccessRuleCommand> SetupSutProvider()
    {
        var sutProvider = new SutProvider<DeleteAccessRuleCommand>()
            .WithFakeTimeProvider()
            .Create();
        sutProvider.GetDependency<FakeTimeProvider>().SetUtcNow(_now);
        return sutProvider;
    }
}
