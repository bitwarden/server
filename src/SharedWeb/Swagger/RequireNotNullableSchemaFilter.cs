using System.Reflection;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Bit.SharedWeb.Swagger;

/// <summary>
///
/// </summary>
/// <remarks>
/// Credits: https://github.com/domaindrivendev/Swashbuckle.AspNetCore/issues/2036#issuecomment-894015122
/// </remarks>
public class RequireNotNullableSchemaFilter : ISchemaFilter
{
    public void Apply(OpenApiSchema schema, SchemaFilterContext context)
    {
        if (schema.Properties == null)
        {
            return;
        }

        FixNullableProperties(schema, context);

        var notNullableProperties = schema
            .Properties
            .Where(x => !x.Value.Nullable && x.Value.Default == default && !schema.Required.Contains(x.Key))
            .ToList();

        foreach (var property in notNullableProperties)
        {
            schema.Required.Add(property.Key);
        }
    }

    /// <summary>
    /// Option "SupportNonNullableReferenceTypes" not working with complex types ({ "type": "object" }),
    /// so they always have "Nullable = false",
    /// see method "SchemaGenerator.GenerateSchemaForMember"
    /// </summary>
    private static void FixNullableProperties(OpenApiSchema schema, SchemaFilterContext context)
    {
        foreach (var property in schema.Properties)
        {
            var field = context.Type
                .GetMembers(BindingFlags.Public | BindingFlags.Instance)
                .FirstOrDefault(x =>
                    string.Equals(x.Name, property.Key, StringComparison.InvariantCultureIgnoreCase));

            if (field == null)
            {
                continue;
            }

            var fieldType = field switch
            {
                FieldInfo fieldInfo => fieldInfo.FieldType,
                PropertyInfo propertyInfo => propertyInfo.PropertyType,
                _ => throw new NotSupportedException(),
            };

            property.Value.Nullable = fieldType.IsValueType
                ? Nullable.GetUnderlyingType(fieldType) != null
                : !field.IsNonNullableReferenceType();
        }
    }
}
