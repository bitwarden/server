using Bit.Core;
using Bit.Pam.Enums;
using Bit.Pam.Models;
using Bit.Pam.Repositories;
using Bit.Services.Pam.Services;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
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
        // The substituted feature service reports every flag off, which is the kill switch's absent-flag default.
        var auditEvent = AnEvent(organizationId);

        await sutProvider.Sut.EmitAsync(auditEvent);

        await sutProvider.GetDependency<IAccessAuditEventRepository>().Received(1).CreateAsync(auditEvent);
    }

    // The kill switch stops the write itself, not merely hides the trail, shedding inserts under store pressure.
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
}
