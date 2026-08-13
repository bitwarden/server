using Fido2NetLib;
using Fido2NetLib.Objects;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Bit.SharedWeb.Swagger;

/// <summary>
/// Adds <c>x-base64url</c> extension to fields known to be serialized as base64url for code generation.
/// </summary>
public class Base64UrlSchemaFilter : ISchemaFilter
{
    public void Apply(IOpenApiSchema schema, SchemaFilterContext context)
    {
        if (context.Type == typeof(AssertionOptions))
        {
            MarkPropertyAsBase64Url(schema, "challenge");
        }
        else if (context.Type == typeof(CredentialCreateOptions))
        {
            MarkPropertyAsBase64Url(schema, "challenge");
        }
        else if (context.Type == typeof(Fido2User))
        {
            MarkPropertyAsBase64Url(schema, "id");
        }
        else if (context.Type == typeof(PublicKeyCredentialDescriptor))
        {
            MarkPropertyAsBase64Url(schema, "id");
        }
    }

    private static void MarkPropertyAsBase64Url(IOpenApiSchema schema, string prop)
    {
        if (schema is not OpenApiSchema openApiSchema)
        {
            return;
        }
        openApiSchema.Properties ??= new Dictionary<string, IOpenApiSchema>();
        openApiSchema.Properties.TryAdd(prop, new OpenApiSchema());
        if (openApiSchema.Properties[prop] is OpenApiSchema propSchema)
        {
            propSchema.Extensions ??= new Dictionary<string, IOpenApiExtension>();
            propSchema.Extensions.Add("x-base64url", new JsonNodeExtension(true));
        }
    }
}
