using System.Text.Json;
using Bit.Core.Utilities;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Bit.SharedWeb.Swagger;

/// <summary>
/// Allows for generated spec/SDK bindings to reflect actual JSON schema for properties decorated
/// with <see cref="RawJsonConverter"/>.
/// </summary>
public class RawJsonSchemaFilter : ISchemaFilter
{
    public void Apply(IOpenApiSchema schema, SchemaFilterContext context)
    {
        if (context.Type == null || schema.Properties == null)
        {
            return;
        }

        foreach (var prop in context.Type.GetProperties())
        {
            if (prop.PropertyType != typeof(string))
            {
                continue;
            }

            var hasRawJsonConverter = prop
                .GetCustomAttributes(typeof(System.Text.Json.Serialization.JsonConverterAttribute), true)
                .OfType<System.Text.Json.Serialization.JsonConverterAttribute>()
                .Any(a => a.ConverterType == typeof(RawJsonConverter));

            if (!hasRawJsonConverter)
            {
                continue;
            }

            var jsonPropName = JsonNamingPolicy.CamelCase.ConvertName(prop.Name);
            if (schema.Properties.TryGetValue(jsonPropName, out var value) && value is OpenApiSchema innerSchema)
            {
                var isNullable = (innerSchema.Type & JsonSchemaType.Null) == JsonSchemaType.Null;
                innerSchema.Type = isNullable
                    ? JsonSchemaType.Object | JsonSchemaType.Null
                    : JsonSchemaType.Object;
            }
        }
    }
}
