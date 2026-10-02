using System.Text.Json;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Routing;
using Microsoft.OpenApi;
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
        var action = GetActionName(context.ApiDescription);
        if (string.IsNullOrEmpty(action)) return;

        operation.Extensions ??= new Dictionary<string, IOpenApiExtension>();
        operation.Extensions.Add("x-action-name", new JsonNodeExtension(action));
        // We can't do case changes in the codegen templates, so we also add the snake_case version of the action name
        operation.Extensions.Add("x-action-name-snake-case", new JsonNodeExtension(JsonNamingPolicy.SnakeCaseLower.ConvertName(action)));
    }

    /// <summary>
    /// Resolves the action name for an operation. MVC controllers expose it as the "action" route value.
    /// Minimal API endpoints have none, so we derive it from the endpoint name set via <c>.WithName(...)</c>
    /// (e.g. "Pam_AccessRequests_GetInbox" → "GetInbox").
    /// </summary>
    private static string? GetActionName(ApiDescription apiDescription)
    {
        if (apiDescription.ActionDescriptor.RouteValues.TryGetValue("action", out var action)
            && !string.IsNullOrEmpty(action))
        {
            return action;
        }

        var endpointName = apiDescription.ActionDescriptor.EndpointMetadata
            .OfType<IEndpointNameMetadata>()
            .LastOrDefault()?.EndpointName;
        if (string.IsNullOrEmpty(endpointName))
        {
            return null;
        }

        var lastSeparator = endpointName.LastIndexOf('_');
        return lastSeparator >= 0 ? endpointName[(lastSeparator + 1)..] : endpointName;
    }
}
