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
public class SendAccessBearerOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var requiresSendPolicy = context.MethodInfo
            .GetCustomAttributes(true)
            .OfType<AuthorizeAttribute>()
            .Any(a => a.Policy == Policies.Send);

        if (!requiresSendPolicy) return;

        operation.Security =
        [
            new OpenApiSecurityRequirement
            {
                {
                    new OpenApiSecurityScheme
                    {
                        Reference = new OpenApiReference
                        {
                            Type = ReferenceType.SecurityScheme,
                            Id = "send-access-bearer"
                        }
                    },
                    []
                }
            }
        ];
    }
}
