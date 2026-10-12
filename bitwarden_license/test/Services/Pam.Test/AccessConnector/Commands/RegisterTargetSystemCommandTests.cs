using Bit.Core.Exceptions;
using Bit.Pam.Entities;
using Bit.Pam.Enums;
using Bit.Pam.Models;
using Bit.Pam.Repositories;
using Bit.Services.Pam.AccessConnector.Commands;
using Bit.Services.Pam.Services;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Xunit;

namespace Bit.Services.Pam.Test.AccessConnector.Commands;

[SutProviderCustomize]
public class RegisterTargetSystemCommandTests
{
    private static readonly DateTime _now = new(2026, 7, 6, 12, 0, 0, DateTimeKind.Utc);

    [Theory, BitAutoData]
    public async Task RegisterAsync_NameMissing_ThrowsBadRequest(Guid organizationId, Guid actingUserId)
    {
        var sutProvider = Setup();

        await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.RegisterAsync(
            organizationId, actingUserId, " ", PamTargetSystemMethod.Manual));

        await sutProvider.GetDependency<IPamTargetSystemRepository>().DidNotReceiveWithAnyArgs().CreateAsync(default!);
    }

    [Theory, BitAutoData]
    public async Task RegisterAsync_HappyPath_CreatesTargetPendingSealWithNoSealedData(
        Guid organizationId, Guid actingUserId, string name)
    {
        var sutProvider = Setup();
        sutProvider.GetDependency<IPamTargetSystemRepository>().CreateAsync(Arg.Any<PamTargetSystem>())
            .Returns(call => Task.FromResult(call.Arg<PamTargetSystem>()));

        var result = await sutProvider.Sut.RegisterAsync(
            organizationId, actingUserId, name, PamTargetSystemMethod.Automatic);

        Assert.Equal(PamTargetSystemMethod.Automatic, result.Method);
        Assert.Equal(PamTargetSystemStatus.PendingSeal, result.Status);
        Assert.Null(result.SealedData);
        await sutProvider.GetDependency<IPamTargetSystemRepository>().Received(1).CreateAsync(Arg.Is<PamTargetSystem>(t =>
            t.OrganizationId == organizationId && t.Name == name && t.CreationDate == _now && t.RevisionDate == _now
            && t.Status == PamTargetSystemStatus.PendingSeal && t.SealedData == null));
    }

    [Theory, BitAutoData]
    public async Task RegisterAsync_ManualMethod_CreatesTargetPendingSeal(
        Guid organizationId, Guid actingUserId, string name)
    {
        var sutProvider = Setup();
        sutProvider.GetDependency<IPamTargetSystemRepository>().CreateAsync(Arg.Any<PamTargetSystem>())
            .Returns(call => Task.FromResult(call.Arg<PamTargetSystem>()));

        var result = await sutProvider.Sut.RegisterAsync(
            organizationId, actingUserId, name, PamTargetSystemMethod.Manual);

        Assert.Equal(PamTargetSystemMethod.Manual, result.Method);
        Assert.Equal(PamTargetSystemStatus.PendingSeal, result.Status);
    }

    [Theory, BitAutoData]
    public async Task RegisterAsync_HappyPath_EmitsAttemptThenOutcome(Guid organizationId, Guid actingUserId, string name)
    {
        var sutProvider = Setup();
        var createdId = Guid.NewGuid();
        sutProvider.GetDependency<IPamTargetSystemRepository>().CreateAsync(Arg.Any<PamTargetSystem>())
            .Returns(call =>
            {
                var target = call.Arg<PamTargetSystem>();
                target.Id = createdId;
                return Task.FromResult(target);
            });

        await sutProvider.Sut.RegisterAsync(organizationId, actingUserId, name, PamTargetSystemMethod.Manual);

        var emitter = sutProvider.GetDependency<IAccessAuditEventEmitter>();
        await emitter.Received(1).EmitAsync(Arg.Is<AccessAuditEventData>(e =>
            e.Kind == AccessAuditEventKind.TargetSystemRegistered && e.Phase == AccessAuditEventPhase.Attempt
            && e.TargetSystemName == name));
        await emitter.Received(1).EmitAsync(Arg.Is<AccessAuditEventData>(e =>
            e.Kind == AccessAuditEventKind.TargetSystemRegistered && e.Phase == AccessAuditEventPhase.Outcome
            && e.TargetSystemId == createdId));
    }

    private static SutProvider<RegisterTargetSystemCommand> Setup()
    {
        var sutProvider = new SutProvider<RegisterTargetSystemCommand>().WithFakeTimeProvider().Create();
        sutProvider.GetDependency<FakeTimeProvider>().SetUtcNow(_now);
        return sutProvider;
    }
}
