using Bit.Core.Auth.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Bit.SharedWeb.Swagger;

/// <summary>
/// Applies send-specific security overrides to the OpenAPI document:
/// <list type="bullet">
///   <item>Replaces the global security requirement with the <c>send-access-bearer</c> scheme
///   for endpoints that require the <see cref="Policies.Send"/> authorization policy, causing
///   the OpenAPI generator to emit an explicit Bearer token parameter rather than injecting the
///   user session token via middleware.</item>
///   <item>Sets <c>security: []</c> on <c>[AllowAnonymous]</c> endpoints in
///   <c>SendsController</c>, so the generator emits no auth requirement for V1 send access
///   endpoints.</item>
/// </list>
/// </summary>
public class SendAccessBearerOperationFilter : IDocumentFilter
{
    public void Apply(OpenApiDocument swaggerDoc, DocumentFilterContext context)
    {
        var sendPolicyPaths = context.ApiDescriptions
            .Where(api => api.ActionDescriptor.EndpointMetadata
                .OfType<AuthorizeAttribute>()
                .Any(a => a.Policy == Policies.Send))
            .Select(api => (
                Method: api.HttpMethod?.ToUpperInvariant(),
                Path: $"/{api.RelativePath?.TrimEnd('/')}"
            ))
            .Where(x => x.Method != null)
            .ToHashSet();

        var anonymousSendPaths = context.ApiDescriptions
            .Where(api =>
                api.ActionDescriptor.RouteValues.TryGetValue("controller", out var controller) &&
                controller == "Sends" &&
                api.ActionDescriptor.EndpointMetadata.OfType<AllowAnonymousAttribute>().Any())
            .Select(api => (
                Method: api.HttpMethod?.ToUpperInvariant(),
                Path: $"/{api.RelativePath?.TrimEnd('/')}"
            ))
            .Where(x => x.Method != null)
            .ToHashSet();

        foreach (var (path, pathItem) in swaggerDoc.Paths)
        {
            if (pathItem.Operations is null) continue;

            foreach (var (method, operation) in pathItem.Operations)
            {
                var key = (method.Method.ToUpperInvariant(), path.TrimEnd('/'));

                if (sendPolicyPaths.Contains(key))
                {
                    operation.Security =
                    [
                        new OpenApiSecurityRequirement
                        {
                            [new OpenApiSecuritySchemeReference("send-access-bearer", swaggerDoc)] = []
                        }
                    ];
                }
                else if (anonymousSendPaths.Contains(key))
                {
                    operation.Security = [];
                }
            }
        }
    }
}
