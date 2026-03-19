using System.Reflection;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Bit.SharedWeb.Swagger;

/// <summary>
/// Removes query parameters generated from action method parameters decorated with
/// <see cref="BindNeverAttribute"/>. Swashbuckle "explodes" complex types without an
/// explicit binding source (e.g. [FromBody]) into individual query parameters before
/// [BindNever] can take effect, so this filter cleans them up. Parameters bound via
/// other sources (body, route, header) are unaffected as they are not exploded.
/// </summary>
public class BindNeverOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        if (operation.Parameters == null)
        {
            return;
        }

        // Collect parameter and property names from [BindNever]-annotated method parameters
        var namesToRemove = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var namesToKeep = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var param in context.MethodInfo.GetParameters())
        {
            if (param.GetCustomAttribute<BindNeverAttribute>() != null)
            {
                namesToRemove.Add(param.Name!);

                // Swashbuckle explodes complex types into one query parameter per property,
                // so we need to remove those as well
                foreach (var property in param.ParameterType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
                {
                    namesToRemove.Add(property.Name);
                }
            }
            else
            {
                // Track names from non-[BindNever] parameters so we don't
                // accidentally remove them if they collide with a property name
                namesToKeep.Add(param.Name!);
            }
        }

        namesToRemove.ExceptWith(namesToKeep);

        // Iterate backwards to safely remove items without shifting indices
        for (var i = operation.Parameters.Count - 1; i >= 0; i--)
        {
            var param = operation.Parameters[i];
            if (param.In == ParameterLocation.Query && param.Name != null && namesToRemove.Contains(param.Name))
            {
                operation.Parameters.RemoveAt(i);
            }
        }
    }
}
