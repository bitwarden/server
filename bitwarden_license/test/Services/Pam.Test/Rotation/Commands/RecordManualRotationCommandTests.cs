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
public class RecordManualRotationCommandTests
{
    private static readonly DateTime _now = new(2026, 7, 6, 12, 0, 0, DateTimeKind.Utc);

    [Theory, BitAutoData]
    public async Task RecordAsync_ConfigMissing_ThrowsNotFound(Guid organizationId, Guid actingUserId, Guid configId)
    {
        var sutProvider = Setup();
        sutProvider.GetDependency<IPamRotationConfigRepository>().GetDetailsByIdAsync(configId)
            .Returns((PamRotationConfigDetails?)null);

        await Assert.ThrowsAsync<NotFoundException>(
            () => sutProvider.Sut.RecordAsync(organizationId, actingUserId, configId));

        await sutProvider.GetDependency<IPamRotationConfigRepository>().DidNotReceiveWithAnyArgs().ReplaceAsync(default!);
    }

    [Theory, BitAutoData]
    public async Task RecordAsync_WrongOrg_ThrowsNotFound(Guid actingUserId, PamRotationConfigDetails details)
    {
        var sutProvider = Setup();
        details.TargetSystemMethod = PamTargetSystemMethod.Manual;
        sutProvider.GetDependency<IPamRotationConfigRepository>().GetDetailsByIdAsync(details.Id).Returns(details);

        await Assert.ThrowsAsync<NotFoundException>(
            () => sutProvider.Sut.RecordAsync(Guid.NewGuid(), actingUserId, details.Id));

        await sutProvider.GetDependency<IPamRotationConfigRepository>().DidNotReceiveWithAnyArgs().ReplaceAsync(default!);
    }

    [Theory, BitAutoData]
    public async Task RecordAsync_AutomaticTarget_ThrowsBadRequest(Guid actingUserId, PamRotationConfigDetails details)
    {
        var sutProvider = Setup();
        details.TargetSystemMethod = PamTargetSystemMethod.Automatic;
        sutProvider.GetDependency<IPamRotationConfigRepository>().GetDetailsByIdAsync(details.Id).Returns(details);

        await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.RecordAsync(details.OrganizationId, actingUserId, details.Id));

        await sutProvider.GetDependency<IPamRotationConfigRepository>().DidNotReceiveWithAnyArgs().ReplaceAsync(default!);
    }

    [Theory, BitAutoData]
    public async Task RecordAsync_HappyPath_SetsLastRotationToNowAndNextFromSchedule(
        Guid actingUserId, PamRotationConfigDetails details)
    {
        var sutProvider = Setup();
        details.TargetSystemMethod = PamTargetSystemMethod.Manual;
        details.NextRotationAt = _now.AddMinutes(-10); // an overdue obligation, about to be discharged
        sutProvider.GetDependency<IPamRotationConfigRepository>().GetDetailsByIdAsync(details.Id).Returns(details);
        var nextOccurrence = _now.AddDays(30);
        sutProvider.GetDependency<IRotationScheduleCalculator>().GetNextOccurrence(details.ScheduleCron, _now)
            .Returns(nextOccurrence);

        await sutProvider.Sut.RecordAsync(details.OrganizationId, actingUserId, details.Id);

        // LastRotationAt = now; the obligation clears by moving NextRotationAt to the next scheduled occurrence.
        await sutProvider.GetDependency<IPamRotationConfigRepository>().Received(1).ReplaceAsync(Arg.Is<PamRotationConfig>(c =>
            c.Id == details.Id && c.LastRotationAt == _now && c.NextRotationAt == nextOccurrence
            && c.RevisionDate == _now));
    }

    [Theory, BitAutoData]
    public async Task RecordAsync_NoSchedule_ClearsNextRotationAt(Guid actingUserId, PamRotationConfigDetails details)
    {
        var sutProvider = Setup();
        details.TargetSystemMethod = PamTargetSystemMethod.Manual;
        details.ScheduleCron = null;
        details.NextRotationAt = _now.AddMinutes(-10);
        sutProvider.GetDependency<IPamRotationConfigRepository>().GetDetailsByIdAsync(details.Id).Returns(details);
        sutProvider.GetDependency<IRotationScheduleCalculator>().GetNextOccurrence(null, _now).Returns((DateTime?)null);

        await sutProvider.Sut.RecordAsync(details.OrganizationId, actingUserId, details.Id);

        await sutProvider.GetDependency<IPamRotationConfigRepository>().Received(1).ReplaceAsync(Arg.Is<PamRotationConfig>(c =>
            c.Id == details.Id && c.LastRotationAt == _now && c.NextRotationAt == null));
    }

    [Theory, BitAutoData]
    public async Task RecordAsync_HappyPath_EmitsAttemptThenOutcomeWithActor(
        Guid actingUserId, PamRotationConfigDetails details)
    {
        var sutProvider = Setup();
        details.TargetSystemMethod = PamTargetSystemMethod.Manual;
        sutProvider.GetDependency<IPamRotationConfigRepository>().GetDetailsByIdAsync(details.Id).Returns(details);

        await sutProvider.Sut.RecordAsync(details.OrganizationId, actingUserId, details.Id);

        var emitter = sutProvider.GetDependency<IAccessAuditEventEmitter>();
        await emitter.Received(1).EmitAsync(Arg.Is<AccessAuditEventData>(e =>
            e.Kind == AccessAuditEventKind.ManualRotationRecorded && e.Phase == AccessAuditEventPhase.Attempt
            && e.RotationConfigId == details.Id && e.ActorId == actingUserId));
        await emitter.Received(1).EmitAsync(Arg.Is<AccessAuditEventData>(e =>
            e.Kind == AccessAuditEventKind.ManualRotationRecorded && e.Phase == AccessAuditEventPhase.Outcome
            && e.RotationConfigId == details.Id && e.ActorId == actingUserId));
    }

    private static SutProvider<RecordManualRotationCommand> Setup()
    {
        var sutProvider = new SutProvider<RecordManualRotationCommand>().WithFakeTimeProvider().Create();
        sutProvider.GetDependency<FakeTimeProvider>().SetUtcNow(_now);
        return sutProvider;
    }
}
