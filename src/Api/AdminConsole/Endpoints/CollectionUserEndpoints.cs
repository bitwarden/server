using System.Security.Claims;
using Bit.Api.AdminConsole.Endpoints.Filters;
using Bit.Api.AdminConsole.Endpoints.Handlers;
using Bit.Api.AdminConsole.Models.Request;
using Bit.Core;
using Bit.Core.Auth.Identity;

namespace Bit.Api.AdminConsole.Endpoints;

/// <summary>
/// Maps the PATCH routes for changing a collection's user access as an add/update/remove delta rather than the
/// full desired list. The bulk route applies the same delta to every listed collection.
/// </summary>
public static class CollectionUserEndpoints
{
    public static void MapCollectionUserEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("organizations/{orgId:guid}/collections");
        group.RequireAuthorization(Policies.Application);
        group.RequireFeature(FeatureFlagKeys.PM12473CollectionUserAccessEndpoint);
        group.AddEndpointFilter<AdminConsoleExceptionHandlerEndpointFilter>();

        group.MapPatch("{id:guid}/users",
            (Guid orgId, Guid id, CollectionUserAccessDeltaRequestModel model, ClaimsPrincipal user,
                    CollectionUserEndpointsHandler handler) =>
                handler.PatchUserAccessAsync(orgId, [id], model.Add, model.Update, model.Remove, user))
            .WithName("PatchCollectionUserAccess");

        group.MapPatch("users",
            (Guid orgId, BulkCollectionUserAccessDeltaRequestModel model, ClaimsPrincipal user,
                    CollectionUserEndpointsHandler handler) =>
                handler.PatchUserAccessAsync(orgId, model.CollectionIds.ToList(), model.Add, model.Update, model.Remove, user))
            .WithName("PatchBulkCollectionUserAccess");
    }
}
