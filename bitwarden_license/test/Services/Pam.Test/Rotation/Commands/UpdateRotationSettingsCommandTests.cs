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
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Xunit;

namespace Bit.Services.Pam.Test.Rotation.Commands;

[SutProviderCustomize]
public class UpdateRotationSettingsCommandTests
{
    private static readonly DateTime _now = new(2026, 7, 6, 12, 0, 0, DateTimeKind.Utc);

    [Theory, BitAutoData]
    public async Task UpdateAsync_ConfigMissing_ThrowsNotFound(Guid organizationId, Guid actingUserId, Guid configId, string cron)
    {
        var sutProvider = Setup();
        sutProvider.GetDependency<IPamRotationConfigRepository>().GetByIdAsync(configId).Returns((PamRotationConfig?)null);

        await Assert.ThrowsAsync<NotFoundException>(
            () => sutProvider.Sut.UpdateAsync(organizationId, actingUserId, configId, cron, false));

        await sutProvider.GetDependency<IPamRotationConfigRepository>().DidNotReceiveWithAnyArgs().ReplaceAsync(default!);
    }

    [Theory, BitAutoData]
    public async Task UpdateAsync_WrongOrg_ThrowsNotFound(Guid actingUserId, PamRotationConfig config, string cron)
    {
        var sutProvider = Setup();
        sutProvider.GetDependency<IPamRotationConfigRepository>().GetByIdAsync(config.Id).Returns(config);

        await Assert.ThrowsAsync<NotFoundException>(
            () => sutProvider.Sut.UpdateAsync(Guid.NewGuid(), actingUserId, config.Id, cron, false));

        await sutProvider.GetDependency<IPamRotationConfigRepository>().DidNotReceiveWithAnyArgs().ReplaceAsync(default!);
    }

    [Theory, BitAutoData]
    public async Task UpdateAsync_InvalidCron_ThrowsBadRequest(Guid actingUserId, PamRotationConfig config, string cron)
    {
        var sutProvider = Setup();
        sutProvider.GetDependency<IPamRotationConfigRepository>().GetByIdAsync(config.Id).Returns(config);
        sutProvider.GetDependency<IRotationScheduleCalculator>()
            .When(c => c.ValidateSchedule(cron, Arg.Any<TimeSpan>()))
            .Do(_ => throw new BadRequestException("The schedule is not a valid cron expression."));

        await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.UpdateAsync(config.OrganizationId, actingUserId, config.Id, cron, false));

        await sutProvider.GetDependency<IPamRotationConfigRepository>().DidNotReceiveWithAnyArgs().ReplaceAsync(default!);
    }

    [Theory, BitAutoData]
    public async Task UpdateAsync_NewCron_RecomputesNextRotationAt(Guid actingUserId, PamRotationConfig config, string newCron)
    {
        var sutProvider = Setup();
        sutProvider.GetDependency<IPamRotationConfigRepository>().GetByIdAsync(config.Id).Returns(config);
        var nextOccurrence = _now.AddHours(6);
        sutProvider.GetDependency<IRotationScheduleCalculator>().GetNextOccurrence(newCron, _now).Returns(nextOccurrence);

        var result = await sutProvider.Sut.UpdateAsync(config.OrganizationId, actingUserId, config.Id, newCron, true);

        Assert.Equal(newCron, result.ScheduleCron);
        Assert.Equal(nextOccurrence, result.NextRotationAt);
        Assert.True(result.RotateOnAccessEnd);
        await sutProvider.GetDependency<IPamRotationConfigRepository>().Received(1).ReplaceAsync(Arg.Is<PamRotationConfig>(c =>
            c.Id == config.Id && c.ScheduleCron == newCron && c.NextRotationAt == nextOccurrence));
    }

    [Theory, BitAutoData]
    public async Task UpdateAsync_NullCron_ClearsNextRotationAt(Guid actingUserId, PamRotationConfig config)
    {
        var sutProvider = Setup();
        config.NextRotationAt = _now.AddDays(1);
        sutProvider.GetDependency<IPamRotationConfigRepository>().GetByIdAsync(config.Id).Returns(config);
        // A null cron is always valid (no scheduled rotation) and GetNextOccurrence returns null for it.
        sutProvider.GetDependency<IRotationScheduleCalculator>().GetNextOccurrence(null, _now).Returns((DateTime?)null);

        var result = await sutProvider.Sut.UpdateAsync(config.OrganizationId, actingUserId, config.Id, null, false);

        Assert.Null(result.ScheduleCron);
        Assert.Null(result.NextRotationAt);
        await sutProvider.GetDependency<IPamRotationConfigRepository>().Received(1).ReplaceAsync(Arg.Is<PamRotationConfig>(c =>
            c.Id == config.Id && c.ScheduleCron == null && c.NextRotationAt == null));
    }

    [Theory, BitAutoData]
    public async Task UpdateAsync_HappyPath_EmitsAttemptThenOutcome(Guid actingUserId, PamRotationConfig config, string cron)
    {
        var sutProvider = Setup();
        sutProvider.GetDependency<IPamRotationConfigRepository>().GetByIdAsync(config.Id).Returns(config);

        await sutProvider.Sut.UpdateAsync(config.OrganizationId, actingUserId, config.Id, cron, false);

        var emitter = sutProvider.GetDependency<IAccessAuditEventEmitter>();
        await emitter.Received(1).EmitAsync(Arg.Is<AccessAuditEventData>(e =>
            e.Kind == AccessAuditEventKind.RotationSettingsUpdated && e.Phase == AccessAuditEventPhase.Attempt
            && e.RotationConfigId == config.Id));
        await emitter.Received(1).EmitAsync(Arg.Is<AccessAuditEventData>(e =>
            e.Kind == AccessAuditEventKind.RotationSettingsUpdated && e.Phase == AccessAuditEventPhase.Outcome
            && e.RotationConfigId == config.Id));
    }

    private static SutProvider<UpdateRotationSettingsCommand> Setup()
    {
        var sutProvider = new SutProvider<UpdateRotationSettingsCommand>().WithFakeTimeProvider().Create();
        sutProvider.GetDependency<FakeTimeProvider>().SetUtcNow(_now);
        sutProvider.GetDependency<IOptions<PamRotationOptions>>().Value.Returns(new PamRotationOptions());
        return sutProvider;
    }
}
