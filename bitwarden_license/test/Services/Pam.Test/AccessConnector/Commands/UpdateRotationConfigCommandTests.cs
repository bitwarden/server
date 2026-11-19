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
        Guid organizationId, Guid actingUserId, Guid configId, string wrappedKey, string cron)
    {
        var sutProvider = Setup();

        await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.UpdateAsync(organizationId, actingUserId, configId, " ", wrappedKey, cron, false));

        await sutProvider.GetDependency<IPamRotationConfigRepository>().DidNotReceiveWithAnyArgs().ReplaceAsync(default!);
    }

    [Theory, BitAutoData]
    public async Task UpdateAsync_WrappedKeyMissing_ThrowsBadRequest(
        Guid organizationId, Guid actingUserId, Guid configId, string sealedData, string cron)
    {
        var sutProvider = Setup();

        await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.UpdateAsync(organizationId, actingUserId, configId, sealedData, " ", cron, false));

        await sutProvider.GetDependency<IPamRotationConfigRepository>().DidNotReceiveWithAnyArgs().ReplaceAsync(default!);
    }

    [Theory, BitAutoData]
    public async Task UpdateAsync_ActiveJobWithChangedWrappedKey_ThrowsBadRequest(
        Guid actingUserId, PamRotationConfigDetails details, string newWrappedKey, string cron)
    {
        var sutProvider = Setup();
        details.HasActiveJob = true;
        sutProvider.GetDependency<IPamRotationConfigRepository>().GetDetailsByIdAsync(details.Id).Returns(details);

        await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.UpdateAsync(
            details.OrganizationId, actingUserId, details.Id, details.SealedData, newWrappedKey, cron, false));

        await sutProvider.GetDependency<IPamRotationConfigRepository>().DidNotReceiveWithAnyArgs().ReplaceAsync(default!);
    }

    [Theory, BitAutoData]
    public async Task UpdateAsync_ConfigMissing_ThrowsNotFound(
        Guid organizationId, Guid actingUserId, Guid configId, string sealedData, string wrappedKey, string cron)
    {
        var sutProvider = Setup();
        sutProvider.GetDependency<IPamRotationConfigRepository>().GetDetailsByIdAsync(configId)
            .Returns((PamRotationConfigDetails?)null);

        await Assert.ThrowsAsync<NotFoundException>(
            () => sutProvider.Sut.UpdateAsync(organizationId, actingUserId, configId, sealedData, wrappedKey, cron, false));

        await sutProvider.GetDependency<IPamRotationConfigRepository>().DidNotReceiveWithAnyArgs().ReplaceAsync(default!);
    }

    [Theory, BitAutoData]
    public async Task UpdateAsync_WrongOrg_ThrowsNotFound(
        Guid actingUserId, PamRotationConfigDetails details, string sealedData, string wrappedKey, string cron)
    {
        var sutProvider = Setup();
        sutProvider.GetDependency<IPamRotationConfigRepository>().GetDetailsByIdAsync(details.Id).Returns(details);

        await Assert.ThrowsAsync<NotFoundException>(
            () => sutProvider.Sut.UpdateAsync(Guid.NewGuid(), actingUserId, details.Id, sealedData, wrappedKey, cron, false));

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
            details.OrganizationId, actingUserId, details.Id, newSealedData, details.WrappedKey, cron, false));

        await sutProvider.GetDependency<IPamRotationConfigRepository>().DidNotReceiveWithAnyArgs().ReplaceAsync(default!);
    }

    [Theory, BitAutoData]
    public async Task UpdateAsync_ActiveJobWithUnchangedSealedData_UpdatesSettings(
        Guid actingUserId, PamRotationConfigDetails details, string newCron)
    {
        var sutProvider = Setup();
        details.HasActiveJob = true;
        sutProvider.GetDependency<IPamRotationConfigRepository>().GetDetailsByIdAsync(details.Id).Returns(details);

        await sutProvider.Sut.UpdateAsync(details.OrganizationId, actingUserId, details.Id, details.SealedData, details.WrappedKey, newCron, true);

        await sutProvider.GetDependency<IPamRotationConfigRepository>().Received(1).ReplaceAsync(Arg.Is<PamRotationConfig>(c =>
            c.Id == details.Id && c.SealedData == details.SealedData && c.ScheduleCron == newCron && c.RotateOnAccessEnd));
    }

    [Theory, BitAutoData]
    public async Task UpdateAsync_InvalidCron_ThrowsBadRequestBeforeWriting(
        Guid actingUserId, PamRotationConfigDetails details, string sealedData, string wrappedKey, string cron)
    {
        var sutProvider = Setup();
        details.HasActiveJob = false;
        sutProvider.GetDependency<IPamRotationConfigRepository>().GetDetailsByIdAsync(details.Id).Returns(details);
        sutProvider.GetDependency<IRotationScheduleCalculator>()
            .When(c => c.ValidateSchedule(cron, Arg.Any<TimeSpan>()))
            .Do(_ => throw new BadRequestException("The schedule is not a valid cron expression."));

        await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.UpdateAsync(details.OrganizationId, actingUserId, details.Id, sealedData, wrappedKey, cron, false));

        await sutProvider.GetDependency<IPamRotationConfigRepository>().DidNotReceiveWithAnyArgs().ReplaceAsync(default!);
        await sutProvider.GetDependency<IAccessAuditEventEmitter>().DidNotReceiveWithAnyArgs().EmitAsync(default!);
    }

    [Theory, BitAutoData]
    public async Task UpdateAsync_HappyPath_WritesAllFieldsAndRecomputesNextRotationAt(
        Guid actingUserId, PamRotationConfigDetails details, string sealedData, string wrappedKey, string newCron)
    {
        var sutProvider = Setup();
        details.HasActiveJob = false;
        details.TargetSystemMethod = PamTargetSystemMethod.Automatic;
        sutProvider.GetDependency<IPamRotationConfigRepository>().GetDetailsByIdAsync(details.Id).Returns(details);
        var nextOccurrence = _now.AddHours(6);
        sutProvider.GetDependency<IRotationScheduleCalculator>().GetNextOccurrence(newCron, _now).Returns(nextOccurrence);

        var result = await sutProvider.Sut.UpdateAsync(
            details.OrganizationId, actingUserId, details.Id, sealedData, wrappedKey, newCron, true);

        Assert.Equal(sealedData, result.SealedData);
        Assert.Equal(wrappedKey, result.WrappedKey);
        Assert.Equal(newCron, result.ScheduleCron);
        Assert.Equal(nextOccurrence, result.NextRotationDate);
        Assert.True(result.RotateOnAccessEnd);
        Assert.Equal(_now, result.RevisionDate);
        await sutProvider.GetDependency<IPamRotationConfigRepository>().Received(1).ReplaceAsync(Arg.Is<PamRotationConfig>(c =>
            c.Id == details.Id && c.SealedData == sealedData && c.WrappedKey == wrappedKey && c.ScheduleCron == newCron
            && c.NextRotationDate == nextOccurrence && c.RevisionDate == _now));
    }

    [Theory, BitAutoData]
    public async Task UpdateAsync_NullCron_ClearsNextRotationAt(
        Guid actingUserId, PamRotationConfigDetails details, string sealedData, string wrappedKey)
    {
        var sutProvider = Setup();
        details.HasActiveJob = false;
        details.NextRotationDate = _now.AddDays(1);
        sutProvider.GetDependency<IPamRotationConfigRepository>().GetDetailsByIdAsync(details.Id).Returns(details);
        sutProvider.GetDependency<IRotationScheduleCalculator>().GetNextOccurrence(null, _now).Returns((DateTime?)null);

        var result = await sutProvider.Sut.UpdateAsync(
            details.OrganizationId, actingUserId, details.Id, sealedData, wrappedKey, null, false);

        Assert.Null(result.ScheduleCron);
        Assert.Null(result.NextRotationDate);
        await sutProvider.GetDependency<IPamRotationConfigRepository>().Received(1).ReplaceAsync(Arg.Is<PamRotationConfig>(c =>
            c.Id == details.Id && c.ScheduleCron == null && c.NextRotationDate == null));
    }

    [Theory]
    [BitAutoData(true)]
    [BitAutoData(false)]
    public async Task UpdateAsync_ManualObligationDue_KeepsNextRotationAt(
        bool enabled, Guid actingUserId, PamRotationConfigDetails details, string sealedData, string wrappedKey,
        string newCron)
    {
        var sutProvider = Setup();
        details.HasActiveJob = false;
        details.Enabled = enabled;
        details.TargetSystemMethod = PamTargetSystemMethod.Manual;
        var due = _now.AddHours(-2);
        details.NextRotationDate = due;
        sutProvider.GetDependency<IPamRotationConfigRepository>().GetDetailsByIdAsync(details.Id).Returns(details);

        var result = await sutProvider.Sut.UpdateAsync(
            details.OrganizationId, actingUserId, details.Id, sealedData, wrappedKey, newCron, true);

        Assert.Equal(due, result.NextRotationDate);
        Assert.Equal(newCron, result.ScheduleCron);
        sutProvider.GetDependency<IRotationScheduleCalculator>().DidNotReceiveWithAnyArgs().GetNextOccurrence(default, default);
        await sutProvider.GetDependency<IPamRotationConfigRepository>().Received(1).ReplaceAsync(Arg.Is<PamRotationConfig>(c =>
            c.Id == details.Id && c.NextRotationDate == due));
    }

    [Theory, BitAutoData]
    public async Task UpdateAsync_ManualObligationDue_NullCron_KeepsNextRotationAt(
        Guid actingUserId, PamRotationConfigDetails details, string sealedData, string wrappedKey)
    {
        var sutProvider = Setup();
        details.HasActiveJob = false;
        details.TargetSystemMethod = PamTargetSystemMethod.Manual;
        details.NextRotationDate = _now;
        sutProvider.GetDependency<IPamRotationConfigRepository>().GetDetailsByIdAsync(details.Id).Returns(details);

        var result = await sutProvider.Sut.UpdateAsync(
            details.OrganizationId, actingUserId, details.Id, sealedData, wrappedKey, null, false);

        Assert.Null(result.ScheduleCron);
        Assert.Equal(_now, result.NextRotationDate);
    }

    [Theory]
    [BitAutoData(PamTargetSystemMethod.Manual, 1)]
    [BitAutoData(PamTargetSystemMethod.Automatic, -2)]
    public async Task UpdateAsync_NoManualObligationDue_RecomputesNextRotationAt(
        PamTargetSystemMethod method, int storedOffsetHours, Guid actingUserId, PamRotationConfigDetails details,
        string sealedData, string wrappedKey, string newCron)
    {
        var sutProvider = Setup();
        details.HasActiveJob = false;
        details.TargetSystemMethod = method;
        details.NextRotationDate = _now.AddHours(storedOffsetHours);
        sutProvider.GetDependency<IPamRotationConfigRepository>().GetDetailsByIdAsync(details.Id).Returns(details);
        var nextOccurrence = _now.AddHours(6);
        sutProvider.GetDependency<IRotationScheduleCalculator>().GetNextOccurrence(newCron, _now).Returns(nextOccurrence);

        var result = await sutProvider.Sut.UpdateAsync(
            details.OrganizationId, actingUserId, details.Id, sealedData, wrappedKey, newCron, false);

        Assert.Equal(nextOccurrence, result.NextRotationDate);
    }

    [Theory, BitAutoData]
    public async Task UpdateAsync_HappyPath_EmitsAttemptThenOutcome(
        Guid actingUserId, PamRotationConfigDetails details, string sealedData, string wrappedKey, string cron)
    {
        var sutProvider = Setup();
        details.HasActiveJob = false;
        sutProvider.GetDependency<IPamRotationConfigRepository>().GetDetailsByIdAsync(details.Id).Returns(details);

        await sutProvider.Sut.UpdateAsync(details.OrganizationId, actingUserId, details.Id, sealedData, wrappedKey, cron, false);

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
