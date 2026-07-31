using System.Security.Claims;
using Bit.Api.AdminConsole.Endpoints.Handlers;
using Bit.Api.AdminConsole.Models.Request;
using Bit.Core;

namespace Bit.Api.AdminConsole.Endpoints;

/// <summary>
/// Maps the PATCH routes for changing collection user access as an add/update/remove delta. The bulk route
/// applies the same delta to every listed collection.
/// </summary>
public static class CollectionUserEndpoints
{
    public static void MapCollectionUserEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("organizations/{orgId:guid}/collections").WithAdminConsoleDefaults();
        group.RequireFeature(FeatureFlagKeys.PM35160CollectionAuthorizationHandlers);

        group.MapPatch("{id:guid}/users",
            (Guid orgId, Guid id, CollectionUserAccessDeltaRequestModel model, ClaimsPrincipal user,
                    CollectionUserEndpointsHandler handler) =>
                handler.PatchUserAccessAsync(orgId, [id], model.Add ?? [], model.Update ?? [], model.Remove ?? [], user))
            .WithName("PatchCollectionUserAccess");

        group.MapPatch("users",
            (Guid orgId, BulkCollectionUserAccessDeltaRequestModel model, ClaimsPrincipal user,
                    CollectionUserEndpointsHandler handler) =>
                handler.PatchUserAccessAsync(orgId, model.CollectionIds?.ToList() ?? [], model.Add ?? [], model.Update ?? [], model.Remove ?? [], user))
            .WithName("PatchBulkCollectionUserAccess");
    }
}
