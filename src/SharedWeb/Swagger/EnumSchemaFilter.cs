using Bit.Core.KeyManagement.Models.Response;
using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Bit.SharedWeb.Swagger;

/// <summary>
/// Adds x-enum-varnames containing the name of enums. Useful for code generation.
///</summary>
/// <remarks>
/// Ideally we would use `oneOf` instead but it's not currently handled well by our swagger generator.
///
/// Credits: https://github.com/domaindrivendev/Swashbuckle.WebApi/issues/1287#issuecomment-655164215
/// </remarks>
public class EnumSchemaFilter : ISchemaFilter
{
    public void Apply(OpenApiSchema schema, SchemaFilterContext context)
    {
        if (context.Type.IsEnum)
        {
            var array = new OpenApiArray();
            array.AddRange(Enum.GetNames(context.Type).Select(n => new OpenApiString(n)));
            schema.Extensions.Add("x-enum-varnames", array);
        }
    }
}

/// <summary>
/// Adds x-sdk-wasm extension into the models that are used in the Bitwarden WASM SDK.
/// </summary>
public class SdkWasmSchemaFilter : ISchemaFilter
{
    public void Apply(OpenApiSchema schema, SchemaFilterContext context)
    {
        var sdkWasmTypes = new[]
        {
            typeof(UserDecryptionResponseModel), typeof(MasterPasswordUnlockResponseModel),
            typeof(MasterPasswordUnlockKdfResponseModel)
        };

        if (sdkWasmTypes.Contains(context.Type))
        {
            schema.Extensions.Add("x-sdk-wasm", new OpenApiBoolean(true));
        }
    }
}
