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
    /// <summary>
    /// Properties carrying <c>Base64UrlConverter</c>, keyed by declaring type. The converter accepts
    /// only the URL-safe alphabet without padding, so a generator that assumes standard Base64
    /// produces payloads the server rejects.
    /// </summary>
    private static readonly Dictionary<Type, string[]> _base64UrlProperties = new()
    {
        [typeof(AssertionOptions)] = ["challenge"],
        [typeof(CredentialCreateOptions)] = ["challenge"],
        [typeof(Fido2User)] = ["id"],
        [typeof(PublicKeyCredentialDescriptor)] = ["id"],
        [typeof(AuthenticatorAttestationRawResponse)] = ["rawId"],
        [typeof(AuthenticatorAttestationRawResponse.AttestationResponse)] =
            ["attestationObject", "clientDataJSON"],
        [typeof(AuthenticatorAssertionRawResponse)] = ["rawId"],
        [typeof(AuthenticatorAssertionRawResponse.AssertionResponse)] =
            ["authenticatorData", "signature", "clientDataJSON", "userHandle"],
        [typeof(AuthenticationExtensionsPRFValues)] = ["first", "second"],
        [typeof(AuthenticationExtensionsLargeBlobInputs)] = ["write"],
        [typeof(AuthenticationExtensionsLargeBlobOutputs)] = ["blob"],
    };

    public void Apply(IOpenApiSchema schema, SchemaFilterContext context)
    {
        if (!_base64UrlProperties.TryGetValue(context.Type, out var properties))
        {
            return;
        }

        foreach (var property in properties)
        {
            MarkPropertyAsBase64Url(schema, property);
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
