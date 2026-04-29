using Bit.Core.Auth.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Bit.SharedWeb.Swagger;

/// <summary>
/// Replaces the global security requirement with the <c>send-access-bearer</c> scheme for
/// endpoints that require the <see cref="Policies.Send"/> authorization policy. This causes
/// the OpenAPI generator to emit an explicit Bearer token parameter rather than injecting the
/// user session token via middleware.
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

        foreach (var (path, pathItem) in swaggerDoc.Paths)
        {
            if (pathItem.Operations is null) continue;

            foreach (var (method, operation) in pathItem.Operations)
            {
                if (!sendPolicyPaths.Contains((method.Method.ToUpperInvariant(), path.TrimEnd('/')))) continue;

                operation.Security =
                [
                    new OpenApiSecurityRequirement
                    {
                        [new OpenApiSecuritySchemeReference("send-access-bearer", swaggerDoc)] = []
                    }
                ];
            }
        }
    }
}
