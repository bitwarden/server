using Bit.SharedWeb.Swagger;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace SharedWeb.Test;

public class SwaggerDocUtil
{
    public static (OpenApiDocument, DocumentFilterContext) CreateDocFromController<T>()
    {
        var type = typeof(T);
        var paths = new OpenApiPaths();
        var descriptions = new List<ApiDescription>();

        // Get all public methods from the TestController
        var methods = type.GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.DeclaredOnly);

        foreach (var method in methods)
        {
            foreach (var attr in method.GetCustomAttributes(false))
            {
                string? relativePath = null;
                OperationType operationType;

                // Extract route and operation type based on attribute type
                switch (attr)
                {
                    case HttpGetAttribute getAttr:
                        relativePath = getAttr.Template;
                        operationType = OperationType.Get;
                        break;
                    case HttpPostAttribute postAttr:
                        relativePath = postAttr.Template;
                        operationType = OperationType.Post;
                        break;
                    case HttpDeleteAttribute deleteAttr:
                        relativePath = deleteAttr.Template;
                        operationType = OperationType.Delete;
                        break;
                    case HttpPutAttribute putAttr:
                        relativePath = putAttr.Template;
                        operationType = OperationType.Put;
                        break;
                    case HttpPatchAttribute patchAttr:
                        relativePath = patchAttr.Template;
                        operationType = OperationType.Patch;
                        break;
                    case SwaggerExcludeAttribute: continue; // Skip SwaggerExcludeAttribute
                    default:
                        throw new InvalidOperationException($"Unsupported method attribute: {attr.GetType().Name}");
                }

                var absolutePath = relativePath.StartsWith('/') ? relativePath : "/" + relativePath;

                // Add the operation to the path
                paths.TryAdd(absolutePath, new OpenApiPathItem());
                paths[absolutePath].Operations[operationType] = new OpenApiOperation();

                descriptions.Add(new ApiDescription
                {
                    ActionDescriptor = new ControllerActionDescriptor { MethodInfo = method },
                    HttpMethod = operationType.ToString().ToUpper(),
                    RelativePath = relativePath
                });
            }
        }

        return (new OpenApiDocument { Paths = paths }, new DocumentFilterContext(descriptions, null, null));
    }
}
