using Bit.SharedWeb.Swagger;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace SharedWeb.Test;

public class ActionNameOperationFilterTest
{
    [Fact]
    public void WithValidActionNameAddsActionNameExtensions()
    {
        // Arrange
        var operation = new OpenApiOperation();
        var actionDescriptor = new ActionDescriptor();
        actionDescriptor.RouteValues["action"] = "GetUsers";

        var apiDescription = new ApiDescription
        {
            ActionDescriptor = actionDescriptor
        };

        var context = new OperationFilterContext(apiDescription, null, null, null);
        var filter = new ActionNameOperationFilter();

        // Act
        filter.Apply(operation, context);

        // Assert
        Assert.True(operation.Extensions.ContainsKey("x-action-name"));
        Assert.True(operation.Extensions.ContainsKey("x-action-name-snake-case"));

        var actionNameExt = operation.Extensions["x-action-name"] as OpenApiString;
        var actionNameSnakeCaseExt = operation.Extensions["x-action-name-snake-case"] as OpenApiString;

        Assert.NotNull(actionNameExt);
        Assert.NotNull(actionNameSnakeCaseExt);
        Assert.Equal("GetUsers", actionNameExt.Value);
        Assert.Equal("get_users", actionNameSnakeCaseExt.Value);
    }

    [Fact]
    public void WithMissingActionRouteValueDoesNotAddExtensions()
    {
        // Arrange
        var operation = new OpenApiOperation();
        var actionDescriptor = new ActionDescriptor();
        // Not setting the "action" route value at all

        var apiDescription = new ApiDescription
        {
            ActionDescriptor = actionDescriptor
        };

        var context = new OperationFilterContext(apiDescription, null, null, null);
        var filter = new ActionNameOperationFilter();

        // Act
        filter.Apply(operation, context);

        // Assert
        Assert.False(operation.Extensions.ContainsKey("x-action-name"));
        Assert.False(operation.Extensions.ContainsKey("x-action-name-snake-case"));
    }
}
