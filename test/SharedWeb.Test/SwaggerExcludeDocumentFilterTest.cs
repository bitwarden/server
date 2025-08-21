using Bit.SharedWeb.Swagger;
using Microsoft.AspNetCore.Mvc;
using Microsoft.OpenApi.Models;

namespace SharedWeb.Test;

public class SwaggerExcludeDocumentFilterTest
{
    private class TestController
    {
        [HttpDelete("test-delete")]
        [HttpPost("test-delete/delete")]
        [SwaggerExclude("POST")]
        public void ActionWithPostExcluded() { }

        [HttpPost("test-post")]
        public void ActionWithoutExclude() { }

        [HttpGet("test-allowed")]
        [HttpGet("test-ignored")]
        [HttpPost("test-ignored")]
        [SwaggerExclude("GET", "ignored")]
        public void ActionWithPathMatch() { }
    }

    [Fact]
    public void SwaggerExcludeRemovesExcludedOperations()
    {
        // Arrange
        var (swaggerDoc, context) = SwaggerDocUtil.CreateDocFromController<TestController>();
        var filter = new SwaggerExcludeDocumentFilter();

        // Act
        filter.Apply(swaggerDoc, context);

        // Assert
        Assert.True(swaggerDoc.Paths["/test-delete"].Operations.ContainsKey(OperationType.Delete));
        Assert.False(swaggerDoc.Paths.ContainsKey("/test-delete/delete"));

        Assert.True(swaggerDoc.Paths["/test-post"].Operations.ContainsKey(OperationType.Post));

        Assert.True(swaggerDoc.Paths["/test-allowed"].Operations.ContainsKey(OperationType.Get));
        Assert.True(swaggerDoc.Paths["/test-ignored"].Operations.ContainsKey(OperationType.Post));
        Assert.False(swaggerDoc.Paths["/test-ignored"].Operations.ContainsKey(OperationType.Get));
    }
}
