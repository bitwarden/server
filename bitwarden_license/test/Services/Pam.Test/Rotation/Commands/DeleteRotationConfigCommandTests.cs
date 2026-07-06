using Bit.Core.Exceptions;
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
public class DeleteRotationConfigCommandTests
{
    private static readonly DateTime _now = new(2026, 7, 6, 12, 0, 0, DateTimeKind.Utc);

    [Theory, BitAutoData]
    public async Task DeleteAsync_ConfigMissing_ThrowsNotFound(Guid organizationId, Guid actingUserId, Guid configId)
    {
        var sutProvider = Setup();
        sutProvider.GetDependency<IPamRotationConfigRepository>().GetDetailsByIdAsync(configId)
            .Returns((PamRotationConfigDetails?)null);

        await Assert.ThrowsAsync<NotFoundException>(
            () => sutProvider.Sut.DeleteAsync(organizationId, actingUserId, configId));

        await sutProvider.GetDependency<IPamRotationConfigRepository>().DidNotReceiveWithAnyArgs()
            .DeleteWithJobsAsync(default);
    }

    [Theory, BitAutoData]
    public async Task DeleteAsync_WrongOrg_ThrowsNotFound(Guid actingUserId, PamRotationConfigDetails details)
    {
        var sutProvider = Setup();
        sutProvider.GetDependency<IPamRotationConfigRepository>().GetDetailsByIdAsync(details.Id).Returns(details);

        await Assert.ThrowsAsync<NotFoundException>(
            () => sutProvider.Sut.DeleteAsync(Guid.NewGuid(), actingUserId, details.Id));

        await sutProvider.GetDependency<IPamRotationConfigRepository>().DidNotReceiveWithAnyArgs()
            .DeleteWithJobsAsync(default);
    }

    [Theory, BitAutoData]
    public async Task DeleteAsync_ActiveJob_ThrowsBadRequest(Guid actingUserId, PamRotationConfigDetails details)
    {
        var sutProvider = Setup();
        details.HasActiveJob = true;
        sutProvider.GetDependency<IPamRotationConfigRepository>().GetDetailsByIdAsync(details.Id).Returns(details);

        await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.DeleteAsync(details.OrganizationId, actingUserId, details.Id));

        await sutProvider.GetDependency<IPamRotationConfigRepository>().DidNotReceiveWithAnyArgs()
            .DeleteWithJobsAsync(default);
    }

    [Theory, BitAutoData]
    public async Task DeleteAsync_HappyPath_CallsDeleteWithJobsAsync(Guid actingUserId, PamRotationConfigDetails details)
    {
        var sutProvider = Setup();
        details.HasActiveJob = false;
        sutProvider.GetDependency<IPamRotationConfigRepository>().GetDetailsByIdAsync(details.Id).Returns(details);

        await sutProvider.Sut.DeleteAsync(details.OrganizationId, actingUserId, details.Id);

        await sutProvider.GetDependency<IPamRotationConfigRepository>().Received(1).DeleteWithJobsAsync(details.Id);
    }

    [Theory, BitAutoData]
    public async Task DeleteAsync_HappyPath_EmitsAttemptThenOutcomeWithPreCapturedNames(
        Guid actingUserId, PamRotationConfigDetails details)
    {
        var sutProvider = Setup();
        details.HasActiveJob = false;
        sutProvider.GetDependency<IPamRotationConfigRepository>().GetDetailsByIdAsync(details.Id).Returns(details);

        await sutProvider.Sut.DeleteAsync(details.OrganizationId, actingUserId, details.Id);

        var emitter = sutProvider.GetDependency<IAccessAuditEventEmitter>();
        await emitter.Received(1).EmitAsync(Arg.Is<AccessAuditEventData>(e =>
            e.Kind == AccessAuditEventKind.RotationConfigDeleted && e.Phase == AccessAuditEventPhase.Attempt
            && e.RotationConfigId == details.Id && e.TargetSystemId == details.TargetSystemId
            && e.TargetSystemName == details.TargetSystemName && e.CipherId == details.CipherId));
        await emitter.Received(1).EmitAsync(Arg.Is<AccessAuditEventData>(e =>
            e.Kind == AccessAuditEventKind.RotationConfigDeleted && e.Phase == AccessAuditEventPhase.Outcome
            && e.RotationConfigId == details.Id && e.TargetSystemName == details.TargetSystemName));
    }

    private static SutProvider<DeleteRotationConfigCommand> Setup()
    {
        var sutProvider = new SutProvider<DeleteRotationConfigCommand>().WithFakeTimeProvider().Create();
        sutProvider.GetDependency<FakeTimeProvider>().SetUtcNow(_now);
        return sutProvider;
    }
}
