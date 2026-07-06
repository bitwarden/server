using Bit.Core.Exceptions;
using Bit.Pam.Entities;
using Bit.Pam.Enums;
using Bit.Pam.Models;
using Bit.Pam.Repositories;
using Bit.Pam.Services;
using Bit.Services.Pam.Rotation;
using Bit.Services.Pam.Rotation.Commands;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Xunit;

namespace Bit.Services.Pam.Test.Rotation.Commands;

[SutProviderCustomize]
public class ResumeRotationCommandTests
{
    private static readonly DateTime _now = new(2026, 7, 6, 12, 0, 0, DateTimeKind.Utc);

    [Theory, BitAutoData]
    public async Task ResumeAsync_ConfigMissing_ThrowsNotFound(Guid organizationId, Guid actingUserId, Guid configId)
    {
        var sutProvider = Setup();
        sutProvider.GetDependency<IPamRotationConfigRepository>().GetDetailsByIdAsync(configId)
            .Returns((PamRotationConfigDetails?)null);

        await Assert.ThrowsAsync<NotFoundException>(
            () => sutProvider.Sut.ResumeAsync(organizationId, actingUserId, configId));

        await sutProvider.GetDependency<IPamRotationConfigRepository>().DidNotReceiveWithAnyArgs().ReplaceAsync(default!);
    }

    [Theory, BitAutoData]
    public async Task ResumeAsync_WrongOrg_ThrowsNotFound(Guid actingUserId, PamRotationConfigDetails details)
    {
        var sutProvider = Setup();
        sutProvider.GetDependency<IPamRotationConfigRepository>().GetDetailsByIdAsync(details.Id).Returns(details);

        await Assert.ThrowsAsync<NotFoundException>(
            () => sutProvider.Sut.ResumeAsync(Guid.NewGuid(), actingUserId, details.Id));

        await sutProvider.GetDependency<IPamRotationConfigRepository>().DidNotReceiveWithAnyArgs().ReplaceAsync(default!);
    }

    [Theory, BitAutoData]
    public async Task ResumeAsync_AlreadyEnabled_ThrowsBadRequest(Guid actingUserId, PamRotationConfigDetails details)
    {
        var sutProvider = Setup();
        details.Enabled = true;
        sutProvider.GetDependency<IPamRotationConfigRepository>().GetDetailsByIdAsync(details.Id).Returns(details);

        await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.ResumeAsync(details.OrganizationId, actingUserId, details.Id));

        await sutProvider.GetDependency<IPamRotationConfigRepository>().DidNotReceiveWithAnyArgs().ReplaceAsync(default!);
    }

    [Theory, BitAutoData]
    public async Task ResumeAsync_ManualWithDueObligation_SetsNextRotationAtToNowWithoutRecomputing(
        Guid actingUserId, PamRotationConfigDetails details)
    {
        var sutProvider = Setup();
        details.Enabled = false;
        details.TargetSystemMethod = PamTargetSystemMethod.Manual;
        details.NextRotationAt = _now.AddMinutes(-5); // due while paused
        sutProvider.GetDependency<IPamRotationConfigRepository>().GetDetailsByIdAsync(details.Id).Returns(details);

        await sutProvider.Sut.ResumeAsync(details.OrganizationId, actingUserId, details.Id);

        await sutProvider.GetDependency<IPamRotationConfigRepository>().Received(1).ReplaceAsync(Arg.Is<PamRotationConfig>(c =>
            c.Id == details.Id && c.Enabled && c.NextRotationAt == _now));
        // The due obligation is pulled to now, not recomputed from the schedule.
        sutProvider.GetDependency<IRotationScheduleCalculator>().DidNotReceiveWithAnyArgs()
            .GetNextOccurrence(default, default);
    }

    [Theory, BitAutoData]
    public async Task ResumeAsync_ManualWithoutDueObligation_Recomputes(Guid actingUserId, PamRotationConfigDetails details)
    {
        var sutProvider = Setup();
        details.Enabled = false;
        details.TargetSystemMethod = PamTargetSystemMethod.Manual;
        details.NextRotationAt = _now.AddDays(1); // not yet due
        sutProvider.GetDependency<IPamRotationConfigRepository>().GetDetailsByIdAsync(details.Id).Returns(details);
        var recomputed = _now.AddDays(2);
        sutProvider.GetDependency<IRotationScheduleCalculator>().GetNextOccurrence(details.ScheduleCron, _now)
            .Returns(recomputed);

        await sutProvider.Sut.ResumeAsync(details.OrganizationId, actingUserId, details.Id);

        await sutProvider.GetDependency<IPamRotationConfigRepository>().Received(1).ReplaceAsync(Arg.Is<PamRotationConfig>(c =>
            c.Id == details.Id && c.NextRotationAt == recomputed));
    }

    [Theory, BitAutoData]
    public async Task ResumeAsync_Automatic_Recomputes(Guid actingUserId, PamRotationConfigDetails details)
    {
        var sutProvider = Setup();
        details.Enabled = false;
        details.TargetSystemMethod = PamTargetSystemMethod.Automatic;
        details.NextRotationAt = _now.AddMinutes(-5); // "due", but automatic never gets pulled to now here
        sutProvider.GetDependency<IPamRotationConfigRepository>().GetDetailsByIdAsync(details.Id).Returns(details);
        var recomputed = _now.AddHours(3);
        sutProvider.GetDependency<IRotationScheduleCalculator>().GetNextOccurrence(details.ScheduleCron, _now)
            .Returns(recomputed);

        await sutProvider.Sut.ResumeAsync(details.OrganizationId, actingUserId, details.Id);

        await sutProvider.GetDependency<IPamRotationConfigRepository>().Received(1).ReplaceAsync(Arg.Is<PamRotationConfig>(c =>
            c.Id == details.Id && c.NextRotationAt == recomputed));
    }

    [Theory, BitAutoData]
    public async Task ResumeAsync_HappyPath_EmitsAttemptThenOutcome(Guid actingUserId, PamRotationConfigDetails details)
    {
        var sutProvider = Setup();
        details.Enabled = false;
        details.TargetSystemMethod = PamTargetSystemMethod.Automatic;
        sutProvider.GetDependency<IPamRotationConfigRepository>().GetDetailsByIdAsync(details.Id).Returns(details);

        await sutProvider.Sut.ResumeAsync(details.OrganizationId, actingUserId, details.Id);

        var emitter = sutProvider.GetDependency<IAccessAuditEventEmitter>();
        await emitter.Received(1).EmitAsync(Arg.Is<AccessAuditEventData>(e =>
            e.Kind == AccessAuditEventKind.RotationResumed && e.Phase == AccessAuditEventPhase.Attempt
            && e.RotationConfigId == details.Id));
        await emitter.Received(1).EmitAsync(Arg.Is<AccessAuditEventData>(e =>
            e.Kind == AccessAuditEventKind.RotationResumed && e.Phase == AccessAuditEventPhase.Outcome
            && e.RotationConfigId == details.Id));
    }

    private static SutProvider<ResumeRotationCommand> Setup()
    {
        var sutProvider = new SutProvider<ResumeRotationCommand>().WithFakeTimeProvider().Create();
        sutProvider.GetDependency<FakeTimeProvider>().SetUtcNow(_now);
        return sutProvider;
    }
}
