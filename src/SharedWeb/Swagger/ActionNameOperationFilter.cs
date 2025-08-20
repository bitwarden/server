#nullable enable


using System.Text.Json;
using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Bit.SharedWeb.Swagger;

/// <summary>
/// When using the swagger schema for code generation, the functions name will be based on the operation ID.
/// This ends up with functions with names like "ControllerName_FunctionName", as the operation IDs are required to be unique,
/// but for functions we really just want the function name. This filter adds the function name as an extension to the operation,
/// so that it can be used in the code generation templates.
/// </summary>
public class ActionNameOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var action = context.ApiDescription.ActionDescriptor.RouteValues["action"];
        if (string.IsNullOrEmpty(action)) return;

        operation.Extensions.Add("x-action-name", new OpenApiString(action));
        // We can't do case changes in the codegen templates, so we also add the snake_case version of the action name
        operation.Extensions.Add("x-action-name-snake-case", new OpenApiString(JsonNamingPolicy.SnakeCaseLower.ConvertName(action)));
    }
}
