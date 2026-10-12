using Bit.Core.Exceptions;
using Bit.Pam.Entities;
using Bit.Pam.Enums;
using Bit.Pam.Models;
using Bit.Pam.Repositories;
using Bit.Services.Pam.AccessConnector;
using Bit.Services.Pam.AccessConnector.Commands;
using Bit.Services.Pam.Services;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Xunit;

namespace Bit.Services.Pam.Test.AccessConnector.Commands;

[SutProviderCustomize]
public class UpdateRotationConfigCommandTests
{
    private static readonly DateTime _now = new(2026, 7, 6, 12, 0, 0, DateTimeKind.Utc);

    [Theory, BitAutoData]
    public async Task UpdateAsync_SealedDataMissing_ThrowsBadRequest(
        Guid organizationId, Guid actingUserId, Guid configId, string cron)
    {
        var sutProvider = Setup();

        await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.UpdateAsync(organizationId, actingUserId, configId, " ", cron, false));

        await sutProvider.GetDependency<IPamRotationConfigRepository>().DidNotReceiveWithAnyArgs().ReplaceAsync(default!);
    }

    [Theory, BitAutoData]
    public async Task UpdateAsync_ConfigMissing_ThrowsNotFound(
        Guid organizationId, Guid actingUserId, Guid configId, string sealedData, string cron)
    {
        var sutProvider = Setup();
        sutProvider.GetDependency<IPamRotationConfigRepository>().GetDetailsByIdAsync(configId)
            .Returns((PamRotationConfigDetails?)null);

        await Assert.ThrowsAsync<NotFoundException>(
            () => sutProvider.Sut.UpdateAsync(organizationId, actingUserId, configId, sealedData, cron, false));

        await sutProvider.GetDependency<IPamRotationConfigRepository>().DidNotReceiveWithAnyArgs().ReplaceAsync(default!);
    }

    [Theory, BitAutoData]
    public async Task UpdateAsync_WrongOrg_ThrowsNotFound(
        Guid actingUserId, PamRotationConfigDetails details, string sealedData, string cron)
    {
        var sutProvider = Setup();
        sutProvider.GetDependency<IPamRotationConfigRepository>().GetDetailsByIdAsync(details.Id).Returns(details);

        await Assert.ThrowsAsync<NotFoundException>(
            () => sutProvider.Sut.UpdateAsync(Guid.NewGuid(), actingUserId, details.Id, sealedData, cron, false));

        await sutProvider.GetDependency<IPamRotationConfigRepository>().DidNotReceiveWithAnyArgs().ReplaceAsync(default!);
    }

    [Theory, BitAutoData]
    public async Task UpdateAsync_ActiveJobWithChangedSealedData_ThrowsBadRequest(
        Guid actingUserId, PamRotationConfigDetails details, string newSealedData, string cron)
    {
        var sutProvider = Setup();
        details.HasActiveJob = true;
        sutProvider.GetDependency<IPamRotationConfigRepository>().GetDetailsByIdAsync(details.Id).Returns(details);

        await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.UpdateAsync(
            details.OrganizationId, actingUserId, details.Id, newSealedData, cron, false));

        await sutProvider.GetDependency<IPamRotationConfigRepository>().DidNotReceiveWithAnyArgs().ReplaceAsync(default!);
    }

    [Theory, BitAutoData]
    public async Task UpdateAsync_ActiveJobWithUnchangedSealedData_UpdatesSettings(
        Guid actingUserId, PamRotationConfigDetails details, string newCron)
    {
        var sutProvider = Setup();
        details.HasActiveJob = true;
        sutProvider.GetDependency<IPamRotationConfigRepository>().GetDetailsByIdAsync(details.Id).Returns(details);

        await sutProvider.Sut.UpdateAsync(details.OrganizationId, actingUserId, details.Id, details.SealedData, newCron, true);

        await sutProvider.GetDependency<IPamRotationConfigRepository>().Received(1).ReplaceAsync(Arg.Is<PamRotationConfig>(c =>
            c.Id == details.Id && c.SealedData == details.SealedData && c.ScheduleCron == newCron && c.RotateOnAccessEnd));
    }

    [Theory, BitAutoData]
    public async Task UpdateAsync_InvalidCron_ThrowsBadRequestBeforeWriting(
        Guid actingUserId, PamRotationConfigDetails details, string sealedData, string cron)
    {
        var sutProvider = Setup();
        details.HasActiveJob = false;
        sutProvider.GetDependency<IPamRotationConfigRepository>().GetDetailsByIdAsync(details.Id).Returns(details);
        sutProvider.GetDependency<IRotationScheduleCalculator>()
            .When(c => c.ValidateSchedule(cron, Arg.Any<TimeSpan>()))
            .Do(_ => throw new BadRequestException("The schedule is not a valid cron expression."));

        await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.UpdateAsync(details.OrganizationId, actingUserId, details.Id, sealedData, cron, false));

        await sutProvider.GetDependency<IPamRotationConfigRepository>().DidNotReceiveWithAnyArgs().ReplaceAsync(default!);
        await sutProvider.GetDependency<IAccessAuditEventEmitter>().DidNotReceiveWithAnyArgs().EmitAsync(default!);
    }

    [Theory, BitAutoData]
    public async Task UpdateAsync_HappyPath_WritesAllFieldsAndRecomputesNextRotationAt(
        Guid actingUserId, PamRotationConfigDetails details, string sealedData, string newCron)
    {
        var sutProvider = Setup();
        details.HasActiveJob = false;
        sutProvider.GetDependency<IPamRotationConfigRepository>().GetDetailsByIdAsync(details.Id).Returns(details);
        var nextOccurrence = _now.AddHours(6);
        sutProvider.GetDependency<IRotationScheduleCalculator>().GetNextOccurrence(newCron, _now).Returns(nextOccurrence);

        var result = await sutProvider.Sut.UpdateAsync(
            details.OrganizationId, actingUserId, details.Id, sealedData, newCron, true);

        Assert.Equal(sealedData, result.SealedData);
        Assert.Equal(newCron, result.ScheduleCron);
        Assert.Equal(nextOccurrence, result.NextRotationAt);
        Assert.True(result.RotateOnAccessEnd);
        Assert.Equal(_now, result.RevisionDate);
        await sutProvider.GetDependency<IPamRotationConfigRepository>().Received(1).ReplaceAsync(Arg.Is<PamRotationConfig>(c =>
            c.Id == details.Id && c.SealedData == sealedData && c.ScheduleCron == newCron
            && c.NextRotationAt == nextOccurrence && c.RevisionDate == _now));
    }

    [Theory, BitAutoData]
    public async Task UpdateAsync_NullCron_ClearsNextRotationAt(
        Guid actingUserId, PamRotationConfigDetails details, string sealedData)
    {
        var sutProvider = Setup();
        details.HasActiveJob = false;
        details.NextRotationAt = _now.AddDays(1);
        sutProvider.GetDependency<IPamRotationConfigRepository>().GetDetailsByIdAsync(details.Id).Returns(details);
        sutProvider.GetDependency<IRotationScheduleCalculator>().GetNextOccurrence(null, _now).Returns((DateTime?)null);

        var result = await sutProvider.Sut.UpdateAsync(
            details.OrganizationId, actingUserId, details.Id, sealedData, null, false);

        Assert.Null(result.ScheduleCron);
        Assert.Null(result.NextRotationAt);
        await sutProvider.GetDependency<IPamRotationConfigRepository>().Received(1).ReplaceAsync(Arg.Is<PamRotationConfig>(c =>
            c.Id == details.Id && c.ScheduleCron == null && c.NextRotationAt == null));
    }

    [Theory, BitAutoData]
    public async Task UpdateAsync_HappyPath_EmitsAttemptThenOutcome(
        Guid actingUserId, PamRotationConfigDetails details, string sealedData, string cron)
    {
        var sutProvider = Setup();
        details.HasActiveJob = false;
        sutProvider.GetDependency<IPamRotationConfigRepository>().GetDetailsByIdAsync(details.Id).Returns(details);

        await sutProvider.Sut.UpdateAsync(details.OrganizationId, actingUserId, details.Id, sealedData, cron, false);

        var emitter = sutProvider.GetDependency<IAccessAuditEventEmitter>();
        await emitter.Received(1).EmitAsync(Arg.Is<AccessAuditEventData>(e =>
            e.Kind == AccessAuditEventKind.RotationConfigUpdated && e.Phase == AccessAuditEventPhase.Attempt
            && e.RotationConfigId == details.Id));
        await emitter.Received(1).EmitAsync(Arg.Is<AccessAuditEventData>(e =>
            e.Kind == AccessAuditEventKind.RotationConfigUpdated && e.Phase == AccessAuditEventPhase.Outcome
            && e.RotationConfigId == details.Id));
    }

    private static SutProvider<UpdateRotationConfigCommand> Setup()
    {
        var sutProvider = new SutProvider<UpdateRotationConfigCommand>().WithFakeTimeProvider().Create();
        sutProvider.GetDependency<FakeTimeProvider>().SetUtcNow(_now);
        sutProvider.GetDependency<IOptions<PamRotationOptions>>().Value.Returns(new PamRotationOptions());
        return sutProvider;
    }
}
