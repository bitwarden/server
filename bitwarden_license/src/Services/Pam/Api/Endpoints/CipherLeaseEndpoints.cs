using System.Security.Claims;
using Bit.Services.Pam.Api.Endpoints.Handlers;
using Bit.Services.Pam.Api.Models.Request;

namespace Bit.Services.Pam.Api.Endpoints;

/// <summary>The <c>leases/ciphers/{id}</c> resource: the per-cipher leasing entry points.</summary>
internal static class CipherLeaseEndpoints
{
    public static RouteGroupBuilder MapCipherLeaseEndpoints(this RouteGroupBuilder group)
    {
        group.WithTags("CipherLease");

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
