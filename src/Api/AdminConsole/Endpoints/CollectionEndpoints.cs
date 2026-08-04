using System.Security.Claims;
using Bit.Api.AdminConsole.Endpoints.Handlers;
using Bit.Api.AdminConsole.Models.Request;
using Bit.Core;

namespace Bit.Api.AdminConsole.Endpoints;

/// <summary>
/// Maps the unified collection routes handled via Minimal API. When
/// <see cref="FeatureFlagKeys.PM35160CollectionAuthorizationHandlers"/> is off the flagged
/// endpoints return 404 and the existing MVC controller answers the request instead.
/// </summary>
public static class CollectionEndpoints
{
    public static void MapCollectionEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPatch("organizations/{orgId:guid}/collections/{id:guid}",
                (Guid orgId, Guid id, UpdateCollectionWithDeltaRequestModel model, ClaimsPrincipal user,
                        UpdateCollectionHandler handler) =>
                    handler.HandleAsync(orgId, id, model, user))
            .RequireFeature(FeatureFlagKeys.PM35160CollectionAuthorizationHandlers)
            .WithAdminConsoleDefaults()
            .WithName("PatchCollectionWithDelta");
    }
}
