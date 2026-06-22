using System.Security.Claims;
using Bit.Commercial.Pam.Api.Endpoints.Handlers;
using Bit.Commercial.Pam.Api.Models.Request;

namespace Bit.Commercial.Pam.Api.Endpoints;

/// <summary>
/// The <c>leases/ciphers/{id}</c> resource: the per-cipher leasing entry points (pre-check, state, submit).
/// Mirrors the routes the former <c>CipherLeaseController</c> served. <c>id</c> is bound from the group's route
/// prefix. The deprecated <c>GET …/cipher</c> read-back is hosted by a small MVC controller in the Api
/// project (it depends on the Api Vault response models).
/// </summary>
internal static class CipherLeaseEndpoints
{
    public static RouteGroupBuilder MapCipherLeaseEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("pre-check", (Guid id, CipherLeaseEndpointsHandler handler, ClaimsPrincipal user) => handler.PreCheck(user, id))
            .WithName("Pam_CipherLease_PreCheck");

        group.MapGet("state", (Guid id, CipherLeaseEndpointsHandler handler, ClaimsPrincipal user) => handler.State(user, id))
            .WithName("Pam_CipherLease_State");

        group.MapPost("",
            (Guid id, AccessRequestCreateRequestModel model, CipherLeaseEndpointsHandler handler, ClaimsPrincipal user) =>
                handler.Post(user, id, model))
            .WithName("Pam_CipherLease_Post");

        return group;
    }
}
