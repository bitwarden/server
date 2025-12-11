#nullable enable

using System.Text.Json;
using Bit.Core.Utilities;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Bit.SharedWeb.Swagger;

/// <summary>
///  Set the format of any strings that are decorated with the <see cref="EncryptedStringAttribute"/> to "x-enc-string".
///  This will allow the generated bindings to use a more appropriate type for encrypted strings.
/// </summary>
public class EncryptedStringSchemaFilter : ISchemaFilter
{
    public void Apply(OpenApiSchema schema, SchemaFilterContext context)
    {
        if (context.Type == null || schema.Properties == null)
            return;

        foreach (var prop in context.Type.GetProperties())
        {
            // Only apply to string properties
            if (prop.PropertyType != typeof(string))
                continue;

            // Check if the property has the EncryptedString attribute
            if (prop.GetCustomAttributes(typeof(EncryptedStringAttribute), true).FirstOrDefault() != null)
            {
                // Convert prop.Name to camelCase for JSON schema property lookup
                var jsonPropName = JsonNamingPolicy.CamelCase.ConvertName(prop.Name);

                if (schema.Properties.TryGetValue(jsonPropName, out var value))
                {
                    value.Format = "x-enc-string";
                }
            }
        }
    }
}
