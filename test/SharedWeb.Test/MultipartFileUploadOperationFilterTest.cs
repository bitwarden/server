using Bit.SharedWeb.Swagger;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace SharedWeb.Test;

public class MultipartFileUploadTestController : ControllerBase
{
    [HttpPost("multipart-upload")]
    [MultipartFileUpload("key")]
    public void Upload() { }
}

public class MultipartFileUploadOperationFilterTest
{
    private static OpenApiOperation Apply(ApiDescription apiDescription)
    {
        var operation = new OpenApiOperation();
        var context = new OperationFilterContext(apiDescription, null, null, null, null);
        new MultipartFileUploadOperationFilter().Apply(operation, context);
        return operation;
    }

    private static OpenApiOperation Apply(params object[] endpointMetadata) =>
        Apply(new ApiDescription
        {
            ActionDescriptor = new ActionDescriptor { EndpointMetadata = endpointMetadata.ToList() },
        });

    [Fact]
    public void AddsRequiredBinaryFilePart()
    {
        var operation = Apply(new MultipartFileUploadAttribute());

        Assert.NotNull(operation.RequestBody);
        Assert.True(operation.RequestBody.Required);
        var schema = operation.RequestBody.Content!["multipart/form-data"].Schema!;
        Assert.Equal(["data"], schema.Properties!.Keys);
        Assert.Equal(JsonSchemaType.String, schema.Properties["data"].Type);
        Assert.Equal("binary", schema.Properties["data"].Format);
        Assert.Equal(["data"], schema.Required!);
    }

    [Fact]
    public void PlacesTextFieldsBeforeFileAndLeavesThemOptional()
    {
        var operation = Apply(new MultipartFileUploadAttribute("key"));

        var schema = operation.RequestBody!.Content!["multipart/form-data"].Schema!;
        Assert.Equal(["key", "data"], schema.Properties!.Keys);
        Assert.Equal(JsonSchemaType.String, schema.Properties["key"].Type);
        Assert.Null(schema.Properties["key"].Format);
        Assert.Equal(["data"], schema.Required!);
    }

    [Fact]
    public void LeavesUnannotatedOperationsUnchanged()
    {
        var operation = Apply();

        Assert.Null(operation.RequestBody);
    }

    [Fact]
    public void ReadsAttributeFromControllerAction()
    {
        var (_, context) = SwaggerDocUtil.CreateDocFromControllers(typeof(MultipartFileUploadTestController));

        var operation = Apply(context.ApiDescriptions.Single());

        var schema = operation.RequestBody!.Content!["multipart/form-data"].Schema!;
        Assert.Equal(["key", "data"], schema.Properties!.Keys);
    }
}
