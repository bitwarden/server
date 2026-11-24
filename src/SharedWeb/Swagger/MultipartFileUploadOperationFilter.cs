using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Bit.SharedWeb.Swagger;

/// <summary>
/// Adds a multipart/form-data request body to actions marked with <see cref="MultipartFileUploadAttribute"/>.
/// Those actions disable form binding to stream large files, which leaves Swashbuckle with no body to document.
/// </summary>
public class MultipartFileUploadOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var attribute = context.ApiDescription?.ActionDescriptor.EndpointMetadata
            .OfType<MultipartFileUploadAttribute>()
            .LastOrDefault();
        if (attribute == null)
        {
            return;
        }

        var properties = new Dictionary<string, IOpenApiSchema>();
        foreach (var field in attribute.TextFields)
        {
            properties[field] = new OpenApiSchema { Type = JsonSchemaType.String };
        }
        properties[attribute.FileField] = new OpenApiSchema { Type = JsonSchemaType.String, Format = "binary" };

        operation.RequestBody = new OpenApiRequestBody
        {
            Required = true,
            Content = new Dictionary<string, OpenApiMediaType>
            {
                ["multipart/form-data"] = new OpenApiMediaType
                {
                    Schema = new OpenApiSchema
                    {
                        Type = JsonSchemaType.Object,
                        Properties = properties,
                        Required = new HashSet<string> { attribute.FileField },
                    },
                },
            },
        };
    }
}
