#nullable enable

using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Bit.SharedWeb.Swagger;

public class SwaggerExcludeDocumentFilter : IDocumentFilter
{
    public void Apply(OpenApiDocument swaggerDoc, DocumentFilterContext context)
    {
        var operationsToRemove = new List<(string pathKey, OperationType operationType)>();

        foreach (var apiDesc in context.ApiDescriptions)
        {
            // Check if the descriptor is a controller action and it has the SwaggerExclude attribute
            if (apiDesc.ActionDescriptor is not ControllerActionDescriptor actionDesc) continue;
            var hideAttributes = actionDesc.MethodInfo.GetCustomAttributes(typeof(SwaggerExcludeAttribute), true) as SwaggerExcludeAttribute[];
            if (hideAttributes == null || hideAttributes.Length == 0) continue;

            var currentHttpMethod = apiDesc.HttpMethod;
            var currentPath = apiDesc.RelativePath;
            if (string.IsNullOrEmpty(currentHttpMethod) || string.IsNullOrEmpty(currentPath)) continue;

            // If the attribute indicates to hide this method, add it to the removal list
            if (hideAttributes.Any(attr => attr.Matches(currentHttpMethod, currentPath)))
            {
                var pathKey = "/" + currentPath;
                var operationType = GetOperationType(currentHttpMethod);
                operationsToRemove.Add((pathKey, operationType));
            }
        }

        // Remove the operations from the swagger document
        foreach (var (pathKey, operationType) in operationsToRemove)
        {
            if (swaggerDoc.Paths.TryGetValue(pathKey, out var pathItem))
            {
                pathItem.Operations.Remove(operationType);

                // If this was the last operation for this path, remove the entire path
                if (!pathItem.Operations.Any())
                {
                    swaggerDoc.Paths.Remove(pathKey);
                }
            }
        }
    }

    private static OperationType GetOperationType(string httpMethod)
    {
        return httpMethod.ToUpper() switch
        {
            "GET" => OperationType.Get,
            "POST" => OperationType.Post,
            "PUT" => OperationType.Put,
            "DELETE" => OperationType.Delete,
            "PATCH" => OperationType.Patch,
            "HEAD" => OperationType.Head,
            "OPTIONS" => OperationType.Options,
            "TRACE" => OperationType.Trace,
            _ => throw new ArgumentException($"Unsupported HTTP method: {httpMethod}")
        };
    }

}
