using Bit.SharedWeb.Swagger;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace SharedWeb.Test;

public class UniqueOperationIdsController : ControllerBase
{
    [HttpGet("unique-get")]
    public void UniqueGetAction() { }

    [HttpPost("unique-post")]
    public void UniquePostAction() { }
}

public class OverloadedOperationIdsController : ControllerBase
{
    [HttpPut("another-duplicate")]
    public void AnotherDuplicateAction() { }

    [HttpPatch("another-duplicate/{id}")]
    public void AnotherDuplicateAction(int id) { }
}

public class MultipleHttpMethodsController : ControllerBase
{
    [HttpGet("multi-method")]
    [HttpPost("multi-method")]
    [HttpPut("multi-method")]
    public void MultiMethodAction() { }
}

public class CheckDuplicateOperationIdsDocumentFilterTest
{
    [Fact]
    public void UniqueOperationIdsDoNotThrowException()
    {
        // Arrange
        var (swaggerDoc, context) = SwaggerDocUtil.CreateDocFromControllers(typeof(UniqueOperationIdsController));
        var filter = new CheckDuplicateOperationIdsDocumentFilter();
        filter.Apply(swaggerDoc, context);
        // Act & Assert
        var exception = Record.Exception(() => filter.Apply(swaggerDoc, context));
        Assert.Null(exception);
    }

    [Fact]
    public void DuplicateOperationIdsThrowInvalidOperationException()
    {
        // Arrange
        var (swaggerDoc, context) = SwaggerDocUtil.CreateDocFromControllers(typeof(OverloadedOperationIdsController));
        var filter = new CheckDuplicateOperationIdsDocumentFilter(false);

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() => filter.Apply(swaggerDoc, context));
        Assert.Contains("Duplicate operation IDs found in Swagger schema", exception.Message);
    }

    [Fact]
    public void MultipleHttpMethodsThrowInvalidOperationException()
    {
        // Arrange
        var (swaggerDoc, context) = SwaggerDocUtil.CreateDocFromControllers(typeof(MultipleHttpMethodsController));
        var filter = new CheckDuplicateOperationIdsDocumentFilter(false);

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() => filter.Apply(swaggerDoc, context));
        Assert.Contains("Duplicate operation IDs found in Swagger schema", exception.Message);
    }

    [Fact]
    public void EmptySwaggerDocDoesNotThrowException()
    {
        // Arrange
        var swaggerDoc = new OpenApiDocument { Paths = [] };
        var context = new DocumentFilterContext([], null, null);
        var filter = new CheckDuplicateOperationIdsDocumentFilter(false);

        // Act & Assert
        var exception = Record.Exception(() => filter.Apply(swaggerDoc, context));
        Assert.Null(exception);
    }
}
