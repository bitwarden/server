using Bit.Core.Exceptions;
using Bit.Core.SecretsManager.Entities;
using Bit.Core.SecretsManager.Repositories;
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
public class RegisterDaemonCommandTests
{
    private static readonly DateTime _now = new(2026, 7, 6, 12, 0, 0, DateTimeKind.Utc);

    [Theory, BitAutoData]
    public async Task RegisterAsync_NameMissing_ThrowsBadRequest(
        Guid organizationId, Guid actingUserId)
    {
        var sutProvider = Setup();

        await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.RegisterAsync(organizationId, actingUserId, "   ", "payload", "key"));

        await sutProvider.GetDependency<IApiKeyRepository>().DidNotReceiveWithAnyArgs().CreateAsync(default!);
        await sutProvider.GetDependency<IPamDaemonRepository>().DidNotReceiveWithAnyArgs().CreateAsync(default!);
    }

    [Theory, BitAutoData]
    public async Task RegisterAsync_HappyPath_CreatesApiKeyWithHashedSecretAndPamDaemon(
        Guid organizationId, Guid actingUserId, string name, string encryptedPayload, string key)
    {
        var sutProvider = Setup();
        var apiKeyId = Guid.NewGuid();
        var daemonId = Guid.NewGuid();
        SetupCreates(sutProvider, apiKeyId, daemonId);

        var result = await sutProvider.Sut.RegisterAsync(organizationId, actingUserId, name, encryptedPayload, key);

        // The ApiKey row is the generic machine credential: ServiceAccountId stays null (this is not an SM key),
        // the scope is the fixed rotation scope, and the stored value is a hash, never the plaintext secret.
        await sutProvider.GetDependency<IApiKeyRepository>().Received(1).CreateAsync(Arg.Is<ApiKey>(k =>
            k.ServiceAccountId == null
            && k.Name == name
            && k.Scope == "[\"api.pam.rotation\"]"
            && k.EncryptedPayload == encryptedPayload
            && k.Key == key
            && !string.IsNullOrEmpty(k.ClientSecretHash)
            && k.ClientSecretHash != result.ClientSecret));

        await sutProvider.GetDependency<IPamDaemonRepository>().Received(1).CreateAsync(Arg.Is<PamDaemon>(d =>
            d.OrganizationId == organizationId
            && d.Name == name
            && d.ApiKeyId == apiKeyId
            && d.Status == PamDaemonStatus.Enabled
            && d.CreationDate == _now
            && d.RevisionDate == _now));

        // The plaintext client secret is only ever available on this one response.
        Assert.False(string.IsNullOrEmpty(result.ClientSecret));
        Assert.Equal(daemonId, result.Daemon.Id);
    }

    [Theory, BitAutoData]
    public async Task RegisterAsync_HappyPath_EmitsAttemptThenOutcome(
        Guid organizationId, Guid actingUserId, string name, string encryptedPayload, string key)
    {
        var sutProvider = Setup();
        var apiKeyId = Guid.NewGuid();
        var daemonId = Guid.NewGuid();
        SetupCreates(sutProvider, apiKeyId, daemonId);

        await sutProvider.Sut.RegisterAsync(organizationId, actingUserId, name, encryptedPayload, key);

        var emitter = sutProvider.GetDependency<IAccessAuditEventEmitter>();
        await emitter.Received(1).EmitAsync(Arg.Is<AccessAuditEventData>(e =>
            e.Kind == AccessAuditEventKind.DaemonRegistered && e.Phase == AccessAuditEventPhase.Attempt
            && e.OrganizationId == organizationId && e.ActorId == actingUserId && e.DaemonName == name));
        await emitter.Received(1).EmitAsync(Arg.Is<AccessAuditEventData>(e =>
            e.Kind == AccessAuditEventKind.DaemonRegistered && e.Phase == AccessAuditEventPhase.Outcome
            && e.DaemonId == daemonId));
    }

    private static SutProvider<RegisterDaemonCommand> Setup()
    {
        var sutProvider = new SutProvider<RegisterDaemonCommand>().WithFakeTimeProvider().Create();
        sutProvider.GetDependency<FakeTimeProvider>().SetUtcNow(_now);
        return sutProvider;
    }

    private static void SetupCreates(SutProvider<RegisterDaemonCommand> sutProvider, Guid apiKeyId, Guid daemonId)
    {
        sutProvider.GetDependency<IApiKeyRepository>().CreateAsync(Arg.Any<ApiKey>())
            .Returns(call =>
            {
                var apiKey = call.Arg<ApiKey>();
                apiKey.Id = apiKeyId;
                return Task.FromResult(apiKey);
            });
        sutProvider.GetDependency<IPamDaemonRepository>().CreateAsync(Arg.Any<PamDaemon>())
            .Returns(call =>
            {
                var daemon = call.Arg<PamDaemon>();
                daemon.Id = daemonId;
                return Task.FromResult(daemon);
            });
    }
}
