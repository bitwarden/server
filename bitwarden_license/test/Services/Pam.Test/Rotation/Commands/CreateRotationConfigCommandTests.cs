using Bit.Core.Exceptions;
using Bit.Core.Vault.Entities;
using Bit.Core.Vault.Repositories;
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
public class CreateRotationConfigCommandTests
{
    private static readonly DateTime _now = new(2026, 7, 6, 12, 0, 0, DateTimeKind.Utc);

    [Theory, BitAutoData]
    public async Task CreateAsync_AccountIdentityMissing_ThrowsBadRequest(
        Guid organizationId, Guid actingUserId, Guid cipherId, Guid targetSystemId)
    {
        var sutProvider = Setup();

        await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.CreateAsync(
            organizationId, actingUserId, cipherId, targetSystemId, " ", false, null, false));

        await sutProvider.GetDependency<IPamRotationConfigRepository>().DidNotReceiveWithAnyArgs().CreateAsync(default!);
    }

    [Theory, BitAutoData]
    public async Task CreateAsync_TargetMissing_ThrowsNotFound(
        Guid organizationId, Guid actingUserId, Guid cipherId, Guid targetSystemId, string accountIdentity)
    {
        var sutProvider = Setup();
        sutProvider.GetDependency<IPamTargetSystemRepository>().GetByIdAsync(targetSystemId).Returns((PamTargetSystem?)null);

        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.CreateAsync(
            organizationId, actingUserId, cipherId, targetSystemId, accountIdentity, false, null, false));

        await sutProvider.GetDependency<IPamRotationConfigRepository>().DidNotReceiveWithAnyArgs().CreateAsync(default!);
    }

    [Theory, BitAutoData]
    public async Task CreateAsync_TargetWrongOrg_ThrowsNotFound(
        Guid actingUserId, Guid cipherId, PamTargetSystem target, string accountIdentity)
    {
        var sutProvider = Setup();
        sutProvider.GetDependency<IPamTargetSystemRepository>().GetByIdAsync(target.Id).Returns(target);

        // target.OrganizationId is an unrelated AutoFixture Guid.
        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.CreateAsync(
            Guid.NewGuid(), actingUserId, cipherId, target.Id, accountIdentity, false, null, false));

        await sutProvider.GetDependency<IPamRotationConfigRepository>().DidNotReceiveWithAnyArgs().CreateAsync(default!);
    }

    [Theory, BitAutoData]
    public async Task CreateAsync_TargetDisabled_ThrowsBadRequest(
        Guid actingUserId, Guid cipherId, PamTargetSystem target, string accountIdentity)
    {
        var sutProvider = Setup();
        target.Status = PamTargetSystemStatus.Disabled;
        sutProvider.GetDependency<IPamTargetSystemRepository>().GetByIdAsync(target.Id).Returns(target);

        await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.CreateAsync(
            target.OrganizationId, actingUserId, cipherId, target.Id, accountIdentity, false, null, false));

        await sutProvider.GetDependency<IPamRotationConfigRepository>().DidNotReceiveWithAnyArgs().CreateAsync(default!);
    }

    [Theory, BitAutoData]
    public async Task CreateAsync_CipherMissing_ThrowsNotFound(
        Guid actingUserId, PamTargetSystem target, Guid cipherId, string accountIdentity)
    {
        var sutProvider = Setup();
        target.Status = PamTargetSystemStatus.Active;
        sutProvider.GetDependency<IPamTargetSystemRepository>().GetByIdAsync(target.Id).Returns(target);
        sutProvider.GetDependency<ICipherRepository>().GetByIdAsync(cipherId).Returns((Cipher?)null);

        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.CreateAsync(
            target.OrganizationId, actingUserId, cipherId, target.Id, accountIdentity, false, null, false));

        await sutProvider.GetDependency<IPamRotationConfigRepository>().DidNotReceiveWithAnyArgs().CreateAsync(default!);
    }

    [Theory, BitAutoData]
    public async Task CreateAsync_CipherWrongOrg_ThrowsNotFound(
        Guid actingUserId, PamTargetSystem target, Cipher cipher, string accountIdentity)
    {
        var sutProvider = Setup();
        target.Status = PamTargetSystemStatus.Active;
        sutProvider.GetDependency<IPamTargetSystemRepository>().GetByIdAsync(target.Id).Returns(target);
        // cipher.OrganizationId is an unrelated AutoFixture Guid, distinct from target.OrganizationId.
        sutProvider.GetDependency<ICipherRepository>().GetByIdAsync(cipher.Id).Returns(cipher);

        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.CreateAsync(
            target.OrganizationId, actingUserId, cipher.Id, target.Id, accountIdentity, false, null, false));

        await sutProvider.GetDependency<IPamRotationConfigRepository>().DidNotReceiveWithAnyArgs().CreateAsync(default!);
    }

    [Theory, BitAutoData]
    public async Task CreateAsync_DuplicateCipherConfig_ThrowsBadRequest(
        Guid actingUserId, PamTargetSystem target, Cipher cipher, PamRotationConfig existing, string accountIdentity)
    {
        var sutProvider = Setup();
        target.Status = PamTargetSystemStatus.Active;
        cipher.OrganizationId = target.OrganizationId;
        sutProvider.GetDependency<IPamTargetSystemRepository>().GetByIdAsync(target.Id).Returns(target);
        sutProvider.GetDependency<ICipherRepository>().GetByIdAsync(cipher.Id).Returns(cipher);
        sutProvider.GetDependency<IPamRotationConfigRepository>().GetByCipherIdAsync(cipher.Id).Returns(existing);

        await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.CreateAsync(
            target.OrganizationId, actingUserId, cipher.Id, target.Id, accountIdentity, false, null, false));

        await sutProvider.GetDependency<IPamRotationConfigRepository>().DidNotReceiveWithAnyArgs().CreateAsync(default!);
    }

    [Theory, BitAutoData]
    public async Task CreateAsync_TerminateSessionsOnManualTarget_ThrowsBadRequest(
        Guid actingUserId, PamTargetSystem target, Cipher cipher, string accountIdentity)
    {
        var sutProvider = Setup();
        target.Status = PamTargetSystemStatus.Active;
        target.Method = PamTargetSystemMethod.Manual;
        cipher.OrganizationId = target.OrganizationId;
        SetupOfferableTarget(sutProvider, target, cipher, null);

        await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.CreateAsync(
            target.OrganizationId, actingUserId, cipher.Id, target.Id, accountIdentity, true, null, false));

        await sutProvider.GetDependency<IPamRotationConfigRepository>().DidNotReceiveWithAnyArgs().CreateAsync(default!);
    }

    [Theory, BitAutoData]
    public async Task CreateAsync_TerminateSessionsOnUnsupportingAutomaticTarget_ThrowsBadRequest(
        Guid actingUserId, PamTargetSystem target, Cipher cipher, string accountIdentity)
    {
        var sutProvider = Setup();
        target.Status = PamTargetSystemStatus.Active;
        target.Method = PamTargetSystemMethod.Automatic;
        target.SupportsSessionTermination = false;
        cipher.OrganizationId = target.OrganizationId;
        SetupOfferableTarget(sutProvider, target, cipher, null);

        await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.CreateAsync(
            target.OrganizationId, actingUserId, cipher.Id, target.Id, accountIdentity, true, null, false));

        await sutProvider.GetDependency<IPamRotationConfigRepository>().DidNotReceiveWithAnyArgs().CreateAsync(default!);
    }

    [Theory, BitAutoData]
    public async Task CreateAsync_CronFloorViolation_ThrowsBadRequest(
        Guid actingUserId, PamTargetSystem target, Cipher cipher, string accountIdentity, string scheduleCron)
    {
        var sutProvider = Setup();
        target.Status = PamTargetSystemStatus.Active;
        cipher.OrganizationId = target.OrganizationId;
        SetupOfferableTarget(sutProvider, target, cipher, null);
        sutProvider.GetDependency<IRotationScheduleCalculator>()
            .When(c => c.ValidateSchedule(scheduleCron, Arg.Any<TimeSpan>()))
            .Do(_ => throw new BadRequestException("The schedule must run no more often than every 15 minutes."));

        await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.CreateAsync(
            target.OrganizationId, actingUserId, cipher.Id, target.Id, accountIdentity, false, scheduleCron, false));

        await sutProvider.GetDependency<IPamRotationConfigRepository>().DidNotReceiveWithAnyArgs().CreateAsync(default!);
    }

    [Theory, BitAutoData]
    public async Task CreateAsync_HappyPath_SetsNextRotationAtFromCalculatorAndEnables(
        Guid actingUserId, PamTargetSystem target, Cipher cipher, string accountIdentity, string scheduleCron)
    {
        var sutProvider = Setup();
        target.Status = PamTargetSystemStatus.Active;
        target.Method = PamTargetSystemMethod.Automatic;
        target.SupportsSessionTermination = true;
        cipher.OrganizationId = target.OrganizationId;
        SetupOfferableTarget(sutProvider, target, cipher, null);
        var nextOccurrence = _now.AddDays(1);
        sutProvider.GetDependency<IRotationScheduleCalculator>().GetNextOccurrence(scheduleCron, _now)
            .Returns(nextOccurrence);
        sutProvider.GetDependency<IPamRotationConfigRepository>().CreateAsync(Arg.Any<PamRotationConfig>())
            .Returns(call => Task.FromResult(call.Arg<PamRotationConfig>()));

        var result = await sutProvider.Sut.CreateAsync(
            target.OrganizationId, actingUserId, cipher.Id, target.Id, accountIdentity, true, scheduleCron, true);

        Assert.Equal(nextOccurrence, result.NextRotationAt);
        Assert.True(result.Enabled);
        Assert.Equal(accountIdentity, result.AccountIdentity);
        Assert.True(result.TerminateSessions);
        Assert.True(result.RotateOnAccessEnd);
        await sutProvider.GetDependency<IPamRotationConfigRepository>().Received(1).CreateAsync(Arg.Is<PamRotationConfig>(c =>
            c.OrganizationId == target.OrganizationId && c.CipherId == cipher.Id && c.TargetSystemId == target.Id
            && c.NextRotationAt == nextOccurrence && c.Enabled));
    }

    [Theory, BitAutoData]
    public async Task CreateAsync_HappyPath_EmitsAttemptThenOutcome(
        Guid actingUserId, PamTargetSystem target, Cipher cipher, string accountIdentity)
    {
        var sutProvider = Setup();
        target.Status = PamTargetSystemStatus.Active;
        cipher.OrganizationId = target.OrganizationId;
        SetupOfferableTarget(sutProvider, target, cipher, null);
        var createdId = Guid.NewGuid();
        sutProvider.GetDependency<IPamRotationConfigRepository>().CreateAsync(Arg.Any<PamRotationConfig>())
            .Returns(call =>
            {
                var config = call.Arg<PamRotationConfig>();
                config.Id = createdId;
                return Task.FromResult(config);
            });

        await sutProvider.Sut.CreateAsync(
            target.OrganizationId, actingUserId, cipher.Id, target.Id, accountIdentity, false, null, false);

        var emitter = sutProvider.GetDependency<IAccessAuditEventEmitter>();
        await emitter.Received(1).EmitAsync(Arg.Is<AccessAuditEventData>(e =>
            e.Kind == AccessAuditEventKind.RotationConfigCreated && e.Phase == AccessAuditEventPhase.Attempt
            && e.CipherId == cipher.Id && e.TargetSystemId == target.Id));
        await emitter.Received(1).EmitAsync(Arg.Is<AccessAuditEventData>(e =>
            e.Kind == AccessAuditEventKind.RotationConfigCreated && e.Phase == AccessAuditEventPhase.Outcome
            && e.RotationConfigId == createdId));
    }

    private static void SetupOfferableTarget(
        SutProvider<CreateRotationConfigCommand> sutProvider, PamTargetSystem target, Cipher cipher, PamRotationConfig? existing)
    {
        sutProvider.GetDependency<IPamTargetSystemRepository>().GetByIdAsync(target.Id).Returns(target);
        sutProvider.GetDependency<ICipherRepository>().GetByIdAsync(cipher.Id).Returns(cipher);
        sutProvider.GetDependency<IPamRotationConfigRepository>().GetByCipherIdAsync(cipher.Id).Returns(existing);
    }

    private static SutProvider<CreateRotationConfigCommand> Setup()
    {
        var sutProvider = new SutProvider<CreateRotationConfigCommand>().WithFakeTimeProvider().Create();
        sutProvider.GetDependency<FakeTimeProvider>().SetUtcNow(_now);
        sutProvider.GetDependency<IOptions<PamRotationOptions>>().Value.Returns(new PamRotationOptions());
        return sutProvider;
    }
}
