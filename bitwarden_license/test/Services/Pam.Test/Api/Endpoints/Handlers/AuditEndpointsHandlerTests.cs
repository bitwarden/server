using Bit.Core;
using Bit.Core.Context;
using Bit.Core.Exceptions;
using Bit.Pam.Enums;
using Bit.Pam.Models;
using Bit.Services.Pam.Api.Endpoints.Handlers;
using Bit.Services.Pam.OrganizationFeatures.Queries.Interfaces;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Bitwarden.Server.Sdk.Features;
using NSubstitute;
using Xunit;

namespace Bit.Services.Pam.Test.Api.Endpoints.Handlers;

[SutProviderCustomize]
public class AuditEndpointsHandlerTests
{
    // The trail is authorized by AccessEventLogs, not by collection management: whoever can read the organization's
    // event logs reads the whole PAM trail. A caller without it must not learn the organization exists.
    [Theory, BitAutoData]
    public async Task GetTrail_WithoutAccessEventLogs_ThrowsNotFound(
        Guid organizationId, SutProvider<AuditEndpointsHandler> sutProvider)
    {
        sutProvider.GetDependency<ICurrentContext>().AccessEventLogs(organizationId).Returns(false);

        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.GetTrail(organizationId));
        await sutProvider.GetDependency<IListAccessAuditTrailQuery>()
            .DidNotReceiveWithAnyArgs()
            .GetTrailAsync(default);
    }

    // PM-42480: with the writes shed, the store stops being a record of the period it claims to cover, so the
    // resource is withdrawn rather than served incomplete. Checked ahead of the permission so the two answers are
    // indistinguishable and a probe cannot use the trail to tell an unaudited organization from an unauthorized one.
    [Theory, BitAutoData]
    public async Task GetTrail_WithSqlAuditLoggingDisabled_ThrowsNotFound(
        Guid organizationId, SutProvider<AuditEndpointsHandler> sutProvider)
    {
        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.PamDisableSqlAuditLogging)
            .Returns(true);
        sutProvider.GetDependency<ICurrentContext>().AccessEventLogs(organizationId).Returns(true);

        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.GetTrail(organizationId));
        await sutProvider.GetDependency<IListAccessAuditTrailQuery>()
            .DidNotReceiveWithAnyArgs()
            .GetTrailAsync(default);
    }

    [Theory, BitAutoData]
    public async Task GetTrail_ProjectsTheTrailForTheRouteOrganization(
        Guid organizationId, SutProvider<AuditEndpointsHandler> sutProvider)
    {
        sutProvider.GetDependency<ICurrentContext>().AccessEventLogs(organizationId).Returns(true);
        sutProvider.GetDependency<IListAccessAuditTrailQuery>()
            .GetTrailAsync(organizationId)
            .Returns(new List<AccessAuditEvent>
            {
                new()
                {
                    Kind = AccessAuditEventKind.RequestApproved,
                    Phase = AccessAuditEventPhase.Outcome,
                    OrganizationId = organizationId,
                    OccurredAt = new DateTime(2026, 8, 18, 9, 0, 0, DateTimeKind.Utc),
                },
            });

        var result = await sutProvider.Sut.GetTrail(organizationId);

        var row = Assert.Single(result.Data);
        Assert.Equal("requestApproved", row.Kind);
        Assert.Equal(organizationId, row.OrganizationId);
    }
}
