using Bit.SharedWeb.Swagger;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace SharedWeb.Test;

public class SourceFileLineOperationFilterTest
{
    private class DummyController
    {
        public void DummyMethod() { }
    }

    [Fact]
    public void AddsSourceFileAndLineExtensionsIfAvailable()
    {
        var methodInfo = typeof(DummyController).GetMethod(nameof(DummyController.DummyMethod));
        var operation = new OpenApiOperation();
        var context = new OperationFilterContext(null, null, null, null, methodInfo);
        var filter = new SourceFileLineOperationFilter();
        filter.Apply(operation, context);

        Assert.True(operation.Extensions.ContainsKey("x-source-file"));
        Assert.True(operation.Extensions.ContainsKey("x-source-line"));
        var fileExt = operation.Extensions["x-source-file"] as JsonNodeExtension;
        var lineExt = operation.Extensions["x-source-line"] as JsonNodeExtension;
        Assert.NotNull(fileExt);
        Assert.NotNull(lineExt);

        Assert.Equal(11, (int)lineExt.Node);
        Assert.StartsWith("test/SharedWeb.Test/", fileExt.Node.ToString());
    }
}
