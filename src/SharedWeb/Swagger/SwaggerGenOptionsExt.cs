using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Bit.SharedWeb.Swagger;

public static class SwaggerGenOptionsExt
{

    public static void InitializeSwaggerFilters(
    this SwaggerGenOptions config, IWebHostEnvironment environment)
    {
        config.SchemaFilter<EnumSchemaFilter>();
        config.SchemaFilter<EncryptedStringSchemaFilter>();
        config.SchemaFilter<Base64UrlSchemaFilter>();

        config.OperationFilter<ActionNameOperationFilter>();
        config.OperationFilter<BindNeverOperationFilter>();
        config.DocumentFilter<SendAccessBearerOperationFilter>();

        // Set the operation ID to the name of the controller followed by the name of the function.
        // Note that the "Controller" suffix for the controllers, and the "Async" suffix for the actions
        // are removed already, so we don't need to do that ourselves.
        config.CustomOperationIds(BuildOperationId);
        // Because we're setting custom operation IDs, we need to ensure that we don't accidentally
        // introduce duplicate IDs, which is against the OpenAPI specification and could lead to issues.
        config.DocumentFilter<CheckDuplicateOperationIdsDocumentFilter>();

        // These two filters require debug symbols/git, so only add them in development mode
        if (environment.IsDevelopment())
        {
            config.DocumentFilter<GitCommitDocumentFilter>();
            config.OperationFilter<SourceFileLineOperationFilter>();
        }
    }

    /// <summary>
    /// Builds the operation ID for an endpoint. MVC controllers produce "{controller}_{action}".
    /// Minimal API endpoints carry no controller/action route values, so we fall back to the endpoint
    /// name assigned via <c>.WithName(...)</c>, and finally to a deterministic id derived from the route.
    /// </summary>
    /// <remarks>
    /// Never returns null or empty. Swashbuckle writes the selector's return value straight onto
    /// <c>operation.OperationId</c> — it does not substitute a default when a custom selector is set — so a
    /// null/empty id would collapse distinct endpoints onto the same id, which
    /// <see cref="CheckDuplicateOperationIdsDocumentFilter"/> rejects, aborting offline spec generation.
    /// </remarks>
    public static string BuildOperationId(ApiDescription apiDescription)
    {
        apiDescription.ActionDescriptor.RouteValues.TryGetValue("controller", out var controller);
        apiDescription.ActionDescriptor.RouteValues.TryGetValue("action", out var action);
        if (!string.IsNullOrEmpty(controller) && !string.IsNullOrEmpty(action))
        {
            return $"{controller}_{action}";
        }

        var endpointName = apiDescription.ActionDescriptor.EndpointMetadata
            .OfType<IEndpointNameMetadata>()
            .LastOrDefault()?.EndpointName;
        if (!string.IsNullOrEmpty(endpointName))
        {
            return endpointName;
        }

        // Last resort for a Minimal API endpoint mapped without .WithName(): derive a stable id from the
        // HTTP method and route template. {method}+{path} is OpenAPI's uniqueness key, so this stays unique
        // per operation and keeps the generated spec valid. HttpMethod defaults to "ANY", so the result is
        // never empty after sanitizing non-identifier characters.
        var method = apiDescription.HttpMethod ?? "ANY";
        var path = apiDescription.RelativePath ?? string.Empty;
        var sanitized = new string($"{method}_{path}".Select(c => char.IsLetterOrDigit(c) ? c : '_').ToArray());
        return sanitized.Trim('_');
    }
}
