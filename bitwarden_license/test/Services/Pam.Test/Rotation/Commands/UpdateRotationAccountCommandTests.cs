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
public class UpdateRotationAccountCommandTests
{
    private static readonly DateTime _now = new(2026, 7, 6, 12, 0, 0, DateTimeKind.Utc);

    [Theory, BitAutoData]
    public async Task UpdateAsync_AccountIdentityMissing_ThrowsBadRequest(Guid organizationId, Guid actingUserId, Guid configId)
    {
        var sutProvider = Setup();

        await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.UpdateAsync(organizationId, actingUserId, configId, " ", false));

        await sutProvider.GetDependency<IPamRotationConfigRepository>().DidNotReceiveWithAnyArgs().ReplaceAsync(default!);
    }

    [Theory, BitAutoData]
    public async Task UpdateAsync_ConfigMissing_ThrowsNotFound(
        Guid organizationId, Guid actingUserId, Guid configId, string accountIdentity)
    {
        var sutProvider = Setup();
        sutProvider.GetDependency<IPamRotationConfigRepository>().GetDetailsByIdAsync(configId)
            .Returns((PamRotationConfigDetails?)null);

        await Assert.ThrowsAsync<NotFoundException>(
            () => sutProvider.Sut.UpdateAsync(organizationId, actingUserId, configId, accountIdentity, false));

        await sutProvider.GetDependency<IPamRotationConfigRepository>().DidNotReceiveWithAnyArgs().ReplaceAsync(default!);
    }

    [Theory, BitAutoData]
    public async Task UpdateAsync_WrongOrg_ThrowsNotFound(
        Guid actingUserId, PamRotationConfigDetails details, string accountIdentity)
    {
        var sutProvider = Setup();
        sutProvider.GetDependency<IPamRotationConfigRepository>().GetDetailsByIdAsync(details.Id).Returns(details);

        await Assert.ThrowsAsync<NotFoundException>(
            () => sutProvider.Sut.UpdateAsync(Guid.NewGuid(), actingUserId, details.Id, accountIdentity, false));

        await sutProvider.GetDependency<IPamRotationConfigRepository>().DidNotReceiveWithAnyArgs().ReplaceAsync(default!);
    }

    [Theory, BitAutoData]
    public async Task UpdateAsync_ActiveJob_ThrowsBadRequest(
        Guid actingUserId, PamRotationConfigDetails details, string accountIdentity)
    {
        var sutProvider = Setup();
        details.HasActiveJob = true;
        sutProvider.GetDependency<IPamRotationConfigRepository>().GetDetailsByIdAsync(details.Id).Returns(details);

        await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.UpdateAsync(details.OrganizationId, actingUserId, details.Id, accountIdentity, false));

        await sutProvider.GetDependency<IPamRotationConfigRepository>().DidNotReceiveWithAnyArgs().ReplaceAsync(default!);
    }

    [Theory, BitAutoData]
    public async Task UpdateAsync_TerminateSessionsOnUnsupportingTarget_ThrowsBadRequest(
        Guid actingUserId, PamRotationConfigDetails details, PamTargetSystem target, string accountIdentity)
    {
        var sutProvider = Setup();
        details.HasActiveJob = false;
        details.TargetSystemMethod = PamTargetSystemMethod.Automatic;
        target.SupportsSessionTermination = false;
        sutProvider.GetDependency<IPamRotationConfigRepository>().GetDetailsByIdAsync(details.Id).Returns(details);
        sutProvider.GetDependency<IPamTargetSystemRepository>().GetByIdAsync(details.TargetSystemId).Returns(target);

        await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.UpdateAsync(details.OrganizationId, actingUserId, details.Id, accountIdentity, true));

        await sutProvider.GetDependency<IPamRotationConfigRepository>().DidNotReceiveWithAnyArgs().ReplaceAsync(default!);
    }

    [Theory, BitAutoData]
    public async Task UpdateAsync_TerminateSessionsOnManualTarget_ThrowsBadRequest(
        Guid actingUserId, PamRotationConfigDetails details, PamTargetSystem target, string accountIdentity)
    {
        var sutProvider = Setup();
        details.HasActiveJob = false;
        details.TargetSystemMethod = PamTargetSystemMethod.Manual;
        target.SupportsSessionTermination = true;
        sutProvider.GetDependency<IPamRotationConfigRepository>().GetDetailsByIdAsync(details.Id).Returns(details);
        sutProvider.GetDependency<IPamTargetSystemRepository>().GetByIdAsync(details.TargetSystemId).Returns(target);

        await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.UpdateAsync(details.OrganizationId, actingUserId, details.Id, accountIdentity, true));

        await sutProvider.GetDependency<IPamRotationConfigRepository>().DidNotReceiveWithAnyArgs().ReplaceAsync(default!);
    }

    [Theory, BitAutoData]
    public async Task UpdateAsync_HappyPath_ReplacesAccountAndTermination(
        Guid actingUserId, PamRotationConfigDetails details, PamTargetSystem target, string accountIdentity)
    {
        var sutProvider = Setup();
        details.HasActiveJob = false;
        details.TargetSystemMethod = PamTargetSystemMethod.Automatic;
        target.SupportsSessionTermination = true;
        sutProvider.GetDependency<IPamRotationConfigRepository>().GetDetailsByIdAsync(details.Id).Returns(details);
        sutProvider.GetDependency<IPamTargetSystemRepository>().GetByIdAsync(details.TargetSystemId).Returns(target);

        var result = await sutProvider.Sut.UpdateAsync(details.OrganizationId, actingUserId, details.Id, accountIdentity, true);

        Assert.Equal(accountIdentity, result.AccountIdentity);
        Assert.True(result.TerminateSessions);
        Assert.Equal(_now, result.RevisionDate);
        await sutProvider.GetDependency<IPamRotationConfigRepository>().Received(1).ReplaceAsync(Arg.Is<PamRotationConfig>(c =>
            c.Id == details.Id && c.AccountIdentity == accountIdentity && c.TerminateSessions
            && c.RevisionDate == _now));
    }

    [Theory, BitAutoData]
    public async Task UpdateAsync_HappyPath_EmitsAttemptThenOutcome(
        Guid actingUserId, PamRotationConfigDetails details, PamTargetSystem target, string accountIdentity)
    {
        var sutProvider = Setup();
        details.HasActiveJob = false;
        sutProvider.GetDependency<IPamRotationConfigRepository>().GetDetailsByIdAsync(details.Id).Returns(details);
        sutProvider.GetDependency<IPamTargetSystemRepository>().GetByIdAsync(details.TargetSystemId).Returns(target);

        await sutProvider.Sut.UpdateAsync(details.OrganizationId, actingUserId, details.Id, accountIdentity, false);

        var emitter = sutProvider.GetDependency<IAccessAuditEventEmitter>();
        await emitter.Received(1).EmitAsync(Arg.Is<AccessAuditEventData>(e =>
            e.Kind == AccessAuditEventKind.RotationAccountUpdated && e.Phase == AccessAuditEventPhase.Attempt
            && e.RotationConfigId == details.Id));
        await emitter.Received(1).EmitAsync(Arg.Is<AccessAuditEventData>(e =>
            e.Kind == AccessAuditEventKind.RotationAccountUpdated && e.Phase == AccessAuditEventPhase.Outcome
            && e.RotationConfigId == details.Id));
    }

    private static SutProvider<UpdateRotationAccountCommand> Setup()
    {
        var sutProvider = new SutProvider<UpdateRotationAccountCommand>().WithFakeTimeProvider().Create();
        sutProvider.GetDependency<FakeTimeProvider>().SetUtcNow(_now);
        return sutProvider;
    }
}
