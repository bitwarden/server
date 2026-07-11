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
public class UpdateTargetSystemPolicyCommandTests
{
    private static readonly DateTime _now = new(2026, 7, 6, 12, 0, 0, DateTimeKind.Utc);

    private static readonly PamPasswordPolicy _policy = new() { MinLength = 12, MaxLength = 64, IncludeDigits = true };

    [Theory, BitAutoData]
    public async Task UpdateAsync_TargetMissing_ThrowsNotFound(Guid organizationId, Guid actingUserId, Guid targetSystemId)
    {
        var sutProvider = Setup();
        sutProvider.GetDependency<IPamTargetSystemRepository>().GetByIdAsync(targetSystemId).Returns((PamTargetSystem?)null);

        await Assert.ThrowsAsync<NotFoundException>(
            () => sutProvider.Sut.UpdateAsync(organizationId, actingUserId, targetSystemId, _policy, true));

        await sutProvider.GetDependency<IPamTargetSystemRepository>().DidNotReceiveWithAnyArgs().ReplaceAsync(default!);
    }

    [Theory, BitAutoData]
    public async Task UpdateAsync_WrongOrg_ThrowsNotFound(Guid actingUserId, PamTargetSystem target)
    {
        var sutProvider = Setup();
        sutProvider.GetDependency<IPamTargetSystemRepository>().GetByIdAsync(target.Id).Returns(target);

        await Assert.ThrowsAsync<NotFoundException>(
            () => sutProvider.Sut.UpdateAsync(Guid.NewGuid(), actingUserId, target.Id, _policy, true));

        await sutProvider.GetDependency<IPamTargetSystemRepository>().DidNotReceiveWithAnyArgs().ReplaceAsync(default!);
    }

    [Theory, BitAutoData]
    public async Task UpdateAsync_ManualTarget_ThrowsBadRequest(Guid actingUserId, PamTargetSystem target)
    {
        var sutProvider = Setup();
        target.Method = PamTargetSystemMethod.Manual;
        sutProvider.GetDependency<IPamTargetSystemRepository>().GetByIdAsync(target.Id).Returns(target);

        await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.UpdateAsync(target.OrganizationId, actingUserId, target.Id, _policy, true));

        await sutProvider.GetDependency<IPamTargetSystemRepository>().DidNotReceiveWithAnyArgs().ReplaceAsync(default!);
    }

    [Theory, BitAutoData]
    public async Task UpdateAsync_WithdrawingTerminationWhileConfigRequiresIt_ThrowsBadRequest(
        Guid actingUserId, PamTargetSystem target)
    {
        var sutProvider = Setup();
        target.Method = PamTargetSystemMethod.Automatic;
        target.SupportsSessionTermination = true;
        sutProvider.GetDependency<IPamTargetSystemRepository>().GetByIdAsync(target.Id).Returns(target);
        sutProvider.GetDependency<IPamRotationConfigRepository>()
            .AnyByTargetSystemWithTerminateSessionsAsync(target.Id).Returns(true);

        // Withdrawing (true -> false) while a config on this target still requires termination must be rejected.
        await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.UpdateAsync(target.OrganizationId, actingUserId, target.Id, _policy, false));

        await sutProvider.GetDependency<IPamTargetSystemRepository>().DidNotReceiveWithAnyArgs().ReplaceAsync(default!);
    }

    [Theory, BitAutoData]
    public async Task UpdateAsync_WithdrawingTerminationWhenNoConfigRequiresIt_Succeeds(
        Guid actingUserId, PamTargetSystem target)
    {
        var sutProvider = Setup();
        target.Method = PamTargetSystemMethod.Automatic;
        target.SupportsSessionTermination = true;
        sutProvider.GetDependency<IPamTargetSystemRepository>().GetByIdAsync(target.Id).Returns(target);
        sutProvider.GetDependency<IPamRotationConfigRepository>()
            .AnyByTargetSystemWithTerminateSessionsAsync(target.Id).Returns(false);

        await sutProvider.Sut.UpdateAsync(target.OrganizationId, actingUserId, target.Id, _policy, false);

        await sutProvider.GetDependency<IPamTargetSystemRepository>().Received(1).ReplaceAsync(Arg.Is<PamTargetSystem>(t =>
            t.Id == target.Id && t.SupportsSessionTermination == false));
    }

    [Theory, BitAutoData]
    public async Task UpdateAsync_HappyPath_UpdatesPolicyAndTermination(Guid actingUserId, PamTargetSystem target)
    {
        var sutProvider = Setup();
        target.Method = PamTargetSystemMethod.Automatic;
        target.SupportsSessionTermination = false;
        sutProvider.GetDependency<IPamTargetSystemRepository>().GetByIdAsync(target.Id).Returns(target);

        await sutProvider.Sut.UpdateAsync(target.OrganizationId, actingUserId, target.Id, _policy, true);

        await sutProvider.GetDependency<IPamTargetSystemRepository>().Received(1).ReplaceAsync(Arg.Is<PamTargetSystem>(t =>
            t.Id == target.Id
            && t.PasswordPolicy == PamPasswordPolicy.Serialize(_policy)
            && t.SupportsSessionTermination == true
            && t.RevisionDate == _now));
    }

    [Theory, BitAutoData]
    public async Task UpdateAsync_HappyPath_EmitsAttemptThenOutcome(Guid actingUserId, PamTargetSystem target)
    {
        var sutProvider = Setup();
        target.Method = PamTargetSystemMethod.Automatic;
        target.SupportsSessionTermination = false;
        sutProvider.GetDependency<IPamTargetSystemRepository>().GetByIdAsync(target.Id).Returns(target);

        await sutProvider.Sut.UpdateAsync(target.OrganizationId, actingUserId, target.Id, _policy, true);

        var emitter = sutProvider.GetDependency<IAccessAuditEventEmitter>();
        await emitter.Received(1).EmitAsync(Arg.Is<AccessAuditEventData>(e =>
            e.Kind == AccessAuditEventKind.TargetSystemPolicyUpdated && e.Phase == AccessAuditEventPhase.Attempt
            && e.TargetSystemId == target.Id));
        await emitter.Received(1).EmitAsync(Arg.Is<AccessAuditEventData>(e =>
            e.Kind == AccessAuditEventKind.TargetSystemPolicyUpdated && e.Phase == AccessAuditEventPhase.Outcome
            && e.TargetSystemId == target.Id));
    }

    private static SutProvider<UpdateTargetSystemPolicyCommand> Setup()
    {
        var sutProvider = new SutProvider<UpdateTargetSystemPolicyCommand>().WithFakeTimeProvider().Create();
        sutProvider.GetDependency<FakeTimeProvider>().SetUtcNow(_now);
        return sutProvider;
    }
}
