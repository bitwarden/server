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
public class RegisterTargetSystemCommandTests
{
    private static readonly DateTime _now = new(2026, 7, 6, 12, 0, 0, DateTimeKind.Utc);

    private static readonly PamPasswordPolicy _policy = new() { MinLength = 12, MaxLength = 64 };

    [Theory, BitAutoData]
    public async Task RegisterAsync_NameMissing_ThrowsBadRequest(Guid organizationId, Guid actingUserId)
    {
        var sutProvider = Setup();

        await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.RegisterAsync(
            organizationId, actingUserId, " ", PamTargetSystemMethod.Manual, null, null, null));

        await sutProvider.GetDependency<IPamTargetSystemRepository>().DidNotReceiveWithAnyArgs().CreateAsync(default!);
    }

    [Theory, BitAutoData]
    public async Task RegisterAsync_AutomaticMissingKind_ThrowsBadRequest(Guid organizationId, Guid actingUserId, string name)
    {
        var sutProvider = Setup();

        await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.RegisterAsync(
            organizationId, actingUserId, name, PamTargetSystemMethod.Automatic, null, _policy, true));

        await sutProvider.GetDependency<IPamTargetSystemRepository>().DidNotReceiveWithAnyArgs().CreateAsync(default!);
    }

    [Theory, BitAutoData]
    public async Task RegisterAsync_ManualWithKindSet_ThrowsBadRequest(Guid organizationId, Guid actingUserId, string name)
    {
        var sutProvider = Setup();

        await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.RegisterAsync(
            organizationId, actingUserId, name, PamTargetSystemMethod.Manual, PamTargetSystemKind.Entra, null, null));

        await sutProvider.GetDependency<IPamTargetSystemRepository>().DidNotReceiveWithAnyArgs().CreateAsync(default!);
    }

    [Theory, BitAutoData]
    public async Task RegisterAsync_AutomaticHappyPath_CreatesTargetWithSerializedPolicy(
        Guid organizationId, Guid actingUserId, string name)
    {
        var sutProvider = Setup();
        sutProvider.GetDependency<IPamTargetSystemRepository>().CreateAsync(Arg.Any<PamTargetSystem>())
            .Returns(call => Task.FromResult(call.Arg<PamTargetSystem>()));

        var result = await sutProvider.Sut.RegisterAsync(
            organizationId, actingUserId, name, PamTargetSystemMethod.Automatic, PamTargetSystemKind.Mssql, _policy, true);

        Assert.Equal(PamTargetSystemMethod.Automatic, result.Method);
        Assert.Equal(PamTargetSystemKind.Mssql, result.Kind);
        Assert.True(result.SupportsSessionTermination);
        Assert.Equal(PamTargetSystemStatus.Active, result.Status);
        Assert.Equal(PamPasswordPolicy.Serialize(_policy), result.PasswordPolicy);
        await sutProvider.GetDependency<IPamTargetSystemRepository>().Received(1).CreateAsync(Arg.Is<PamTargetSystem>(t =>
            t.OrganizationId == organizationId && t.Name == name && t.CreationDate == _now && t.RevisionDate == _now));
    }

    [Theory, BitAutoData]
    public async Task RegisterAsync_ManualHappyPath_CreatesTargetWithNoPolicy(
        Guid organizationId, Guid actingUserId, string name)
    {
        var sutProvider = Setup();
        sutProvider.GetDependency<IPamTargetSystemRepository>().CreateAsync(Arg.Any<PamTargetSystem>())
            .Returns(call => Task.FromResult(call.Arg<PamTargetSystem>()));

        var result = await sutProvider.Sut.RegisterAsync(
            organizationId, actingUserId, name, PamTargetSystemMethod.Manual, null, null, null);

        Assert.Equal(PamTargetSystemMethod.Manual, result.Method);
        Assert.Null(result.Kind);
        Assert.Null(result.SupportsSessionTermination);
        Assert.Null(result.PasswordPolicy);
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

        await sutProvider.Sut.RegisterAsync(organizationId, actingUserId, name, PamTargetSystemMethod.Manual, null, null, null);

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
