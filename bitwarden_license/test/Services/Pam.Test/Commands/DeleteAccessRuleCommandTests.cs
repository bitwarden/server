using Bit.Core.Exceptions;
using Bit.Pam.Entities;
using Bit.Pam.Enums;
using Bit.Pam.Models;
using Bit.Pam.Repositories;
using Bit.Pam.Services;
using Bit.Services.Pam.OrganizationFeatures.Commands;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Services.Pam.Test.Commands;

[SutProviderCustomize]
public class DeleteAccessRuleCommandTests
{
    [Theory, BitAutoData]
    public async Task DeleteAsync_HappyPath_SoftDeletes(
        AccessRule existing, Guid deletedBy, SutProvider<DeleteAccessRuleCommand> sutProvider)
    {
        sutProvider.GetDependency<IAccessRuleRepository>()
            .GetByIdAsync(existing.Id)
            .Returns(existing);

        await sutProvider.Sut.DeleteAsync(existing.OrganizationId, existing.Id, deletedBy);

        await sutProvider.GetDependency<IAccessRuleRepository>().Received(1)
            .DeleteAsync(existing);
    }

    [Theory, BitAutoData]
    public async Task DeleteAsync_HappyPath_EmitsAttemptThenOutcome(
        AccessRule existing, Guid deletedBy, SutProvider<DeleteAccessRuleCommand> sutProvider)
    {
        sutProvider.GetDependency<IAccessRuleRepository>()
            .GetByIdAsync(existing.Id)
            .Returns(existing);

        await sutProvider.Sut.DeleteAsync(existing.OrganizationId, existing.Id, deletedBy);

        var emitter = sutProvider.GetDependency<IAccessAuditEventEmitter>();
        await emitter.Received(1).EmitAsync(Arg.Is<AccessAuditEventData>(e =>
            e.Kind == AccessAuditEventKind.RuleDeleted && e.Phase == AccessAuditEventPhase.Attempt
            && e.AccessRuleId == existing.Id && e.ActorId == deletedBy));
        await emitter.Received(1).EmitAsync(Arg.Is<AccessAuditEventData>(e =>
            e.Kind == AccessAuditEventKind.RuleDeleted && e.Phase == AccessAuditEventPhase.Outcome
            && e.AccessRuleId == existing.Id && e.RuleName == existing.Name));
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
    }
}
