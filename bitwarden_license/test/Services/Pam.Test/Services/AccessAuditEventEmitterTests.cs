using Bit.Core;
using Bit.Pam.Enums;
using Bit.Pam.Models;
using Bit.Pam.Repositories;
using Bit.Services.Pam.Services;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace Bit.Services.Pam.Test.Services;

[SutProviderCustomize]
public class AccessAuditEventEmitterTests
{
    private static AccessAuditEventData AnEvent(Guid organizationId) => new()
    {
        Kind = AccessAuditEventKind.RequestSubmitted,
        Phase = AccessAuditEventPhase.Outcome,
        OccurredDate = DateTime.UtcNow,
        OrganizationId = organizationId,
    };

    [Theory, BitAutoData]
    public async Task EmitAsync_PersistsEventToTheStore(
        Guid organizationId, SutProvider<AccessAuditEventEmitter> sutProvider)
    {
        // The substitute reports every flag off.
        var auditEvent = AnEvent(organizationId);

        await sutProvider.Sut.EmitAsync(auditEvent);

        await sutProvider.GetDependency<IAccessAuditEventRepository>().Received(1).CreateAsync(auditEvent);
    }

    // The kill switch stops the write.
    [Theory, BitAutoData]
    public async Task EmitAsync_WithSqlAuditLoggingDisabled_WritesNothing(
        Guid organizationId, SutProvider<AccessAuditEventEmitter> sutProvider)
    {
        sutProvider.GetDependency<Bitwarden.Server.Sdk.Features.IFeatureService>()
            .IsEnabled(FeatureFlagKeys.PamDisableSqlAuditLogging)
            .Returns(true);

        await sutProvider.Sut.EmitAsync(AnEvent(organizationId));

        await sutProvider.GetDependency<IAccessAuditEventRepository>()
            .DidNotReceiveWithAnyArgs()
            .CreateAsync(default!);
    }

    // A failed attempt stops the action.
    [Theory, BitAutoData]
    public async Task EmitAsync_AttemptStoreFailure_Throws(
        Guid organizationId, SutProvider<AccessAuditEventEmitter> sutProvider)
    {
        var auditEvent = AnEvent(organizationId) with { Phase = AccessAuditEventPhase.Attempt };
        sutProvider.GetDependency<IAccessAuditEventRepository>().CreateAsync(auditEvent)
            .ThrowsAsync(new InvalidOperationException());

        await Assert.ThrowsAsync<InvalidOperationException>(() => sutProvider.Sut.EmitAsync(auditEvent));
    }

    // The action has already happened, so a failed outcome does not fail it.
    [Theory, BitAutoData]
    public async Task EmitAsync_OutcomeStoreFailure_DoesNotThrow(
        Guid organizationId, SutProvider<AccessAuditEventEmitter> sutProvider)
    {
        var auditEvent = AnEvent(organizationId) with { Phase = AccessAuditEventPhase.Outcome };
        sutProvider.GetDependency<IAccessAuditEventRepository>().CreateAsync(auditEvent)
            .ThrowsAsync(new InvalidOperationException());

        await sutProvider.Sut.EmitAsync(auditEvent);
    }
}
