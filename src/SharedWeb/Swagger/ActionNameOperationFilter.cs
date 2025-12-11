using System.Text.Json;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Bit.SharedWeb.Swagger;

/// <summary>
/// Adds the action name (function name) as an extension to each operation in the Swagger document.
/// This can be useful for the code generation process, to generate more meaningful names for operations.
/// Note that we add both the original action name and a snake_case version, as the codegen templates
/// cannot do case conversions.
/// </summary>
public class ActionNameOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        if (!context.ApiDescription.ActionDescriptor.RouteValues.TryGetValue("action", out var action)) return;
        if (string.IsNullOrEmpty(action)) return;

        operation.Extensions.Add("x-action-name", new OpenApiString(action));
        // We can't do case changes in the codegen templates, so we also add the snake_case version of the action name
        operation.Extensions.Add("x-action-name-snake-case", new OpenApiString(JsonNamingPolicy.SnakeCaseLower.ConvertName(action)));
    }
}
