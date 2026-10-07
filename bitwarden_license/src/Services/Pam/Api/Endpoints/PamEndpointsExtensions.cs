using Bit.Core;
using Bit.Core.Auth.Identity;
using Bit.ExceptionHandling;
using Bit.OrganizationAuthorization;
using Bit.Services.Pam.AccessConnector.Api.Authorization;
using Bit.Services.Pam.AccessConnector.Api.Endpoints;
using Bit.Services.Pam.AccessConnector.Api.Endpoints.Filters;
using Bit.Services.Pam.AccessConnector.Rotation.Api.Endpoints;
using Bit.Services.Pam.Api.Endpoints.Filters;

namespace Bit.Services.Pam.Api.Endpoints;

/// <summary>Maps the PAM HTTP surface as Minimal API endpoint groups.</summary>
public static class PamEndpointsExtensions
{
    public static void MapPamEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGroup("/leases").WithPamDefaults().MapLeaseEndpoints();
        endpoints.MapGroup("/organizations/{orgId:guid}/audit").WithPamDefaults().MapAuditEndpoints();
        endpoints.MapGroup("/access-requests").WithPamDefaults().MapAccessRequestEndpoints();
        endpoints.MapGroup("/organizations/{orgId:guid}/access-rules").WithPamDefaults().MapAccessRuleEndpoints();
        endpoints.MapGroup("/leases/ciphers/{id:guid}").WithPamDefaults().MapCipherLeaseEndpoints();

        var connectorAdmin = endpoints.MapGroup("/organizations/{orgId:guid}/access-connectors")
            .WithPamAccessConnectorAdminDefaults();
        connectorAdmin.MapAccessConnectorEndpoints();
        connectorAdmin.MapGroup("/rotation/target-systems").MapTargetSystemEndpoints();
        connectorAdmin.MapGroup("/rotation/configs").MapRotationConfigEndpoints();

        var connector = endpoints.MapGroup("/access-connectors").WithPamAccessConnectorMachineDefaults();
        connector.MapGroup("/rotation/jobs").MapRotationJobEndpoints();
        connector.MapGroup("/rotation/attempts").MapRotationAttemptEndpoints();
    }

    private static RouteGroupBuilder WithPamDefaults(this RouteGroupBuilder group) =>
        group.WithPamDefaults(Policies.Application, FeatureFlagKeys.Pam);

    /// <summary>
    /// Authorized in the middleware by <see cref="ManageAccessConnectorRequirement"/>, so handlers and commands only
    /// check that an id reached by route belongs to the route's organization.
    /// </summary>
    private static RouteGroupBuilder WithPamAccessConnectorAdminDefaults(this RouteGroupBuilder group)
    {
        group.WithPamDefaults(Policies.Application, FeatureFlagKeys.PamAccessConnector);
        group.RequireAuthorization(new AuthorizeAttribute<ManageAccessConnectorRequirement>());
        return group;
    }

    /// <summary>
    /// <see cref="AccessConnectorHeartbeatEndpointFilter"/> is added last, so a disabled flag or malformed body
    /// short-circuits ahead of the heartbeat write. The organization comes from the connector's token.
    ///
    /// TODO(PM-39040): rate-limit this group by client_id.
    /// </summary>
    private static RouteGroupBuilder WithPamAccessConnectorMachineDefaults(this RouteGroupBuilder group) =>
        group.WithPamDefaults(Policies.AccessConnector, FeatureFlagKeys.PamAccessConnector)
            .AddEndpointFilter<AccessConnectorHeartbeatEndpointFilter>();

    /// <summary>
    /// The exception filter is added before the others, so it also translates throws from the feature gate and the
    /// validation filter into <c>ErrorResponseModel</c>.
    /// </summary>
    private static RouteGroupBuilder WithPamDefaults(this RouteGroupBuilder group, string policy, string featureFlagKey)
    {
        group.RequireAuthorization(policy);
        group.WithBasicExceptionHandling();
        group.RequireFeature(featureFlagKey);
        group.AddEndpointFilter<PamValidationEndpointFilter>();
        group.WithGroupName("internal");
        return group;
    }
}
