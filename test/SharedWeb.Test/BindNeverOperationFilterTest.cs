using Bit.SharedWeb.Swagger;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace SharedWeb.Test;

public class BindNeverOperationFilterTest
{
    private class ComplexType
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
    }

    private class TestController
    {
        public void WithBindNeverComplexType([BindNever] ComplexType user) { }
        public void WithBindNeverString([BindNever] string test) { }
        public void WithoutBindNever(string normalParam) { }
        public void WithMixed([BindNever] ComplexType user, string keepMe) { }
        public void WithCollidingName([BindNever] ComplexType user, string name) { }
        public void WithCollidingNameReversed(string name, [BindNever] ComplexType user) { }
        public void WithFromBody([BindNever] ComplexType user, [FromBody] ComplexType body) { }
    }

    private static OpenApiOperation MakeOperation(params string[] paramNames)
    {
        var op = new OpenApiOperation
        {
            Parameters = new List<IOpenApiParameter>()
        };
        foreach (var name in paramNames)
        {
            op.Parameters.Add(new OpenApiParameter { Name = name, In = ParameterLocation.Query });
        }
        return op;
    }

    [Fact]
    public void RemovesExplodedComplexTypeParameters()
    {
        var methodInfo = typeof(TestController).GetMethod(nameof(TestController.WithBindNeverComplexType));
        var operation = MakeOperation("id", "name", "email");

        var context = new OperationFilterContext(null, null, null, null, methodInfo);
        new BindNeverOperationFilter().Apply(operation, context);

        Assert.Empty(operation.Parameters);
    }

    [Fact]
    public void RemovesSimpleTypeParameter()
    {
        var methodInfo = typeof(TestController).GetMethod(nameof(TestController.WithBindNeverString));
        var operation = MakeOperation("test");

        var context = new OperationFilterContext(null, null, null, null, methodInfo);
        new BindNeverOperationFilter().Apply(operation, context);

        Assert.Empty(operation.Parameters);
    }

    [Fact]
    public void DoesNotRemoveParametersWithoutBindNever()
    {
        var methodInfo = typeof(TestController).GetMethod(nameof(TestController.WithoutBindNever));
        var operation = MakeOperation("normalParam");

        var context = new OperationFilterContext(null, null, null, null, methodInfo);
        new BindNeverOperationFilter().Apply(operation, context);

        Assert.Single(operation.Parameters);
    }

    [Fact]
    public void PreservesNonBindNeverParametersInMixedCase()
    {
        var methodInfo = typeof(TestController).GetMethod(nameof(TestController.WithMixed));
        var operation = MakeOperation("id", "name", "email", "keepMe");

        var context = new OperationFilterContext(null, null, null, null, methodInfo);
        new BindNeverOperationFilter().Apply(operation, context);

        var remaining = Assert.Single(operation.Parameters);
        Assert.Equal("keepMe", remaining.Name);
    }

    [Fact]
    public void PreservesParameterWhenNameCollidesWithBindNeverProperty()
    {
        var methodInfo = typeof(TestController).GetMethod(nameof(TestController.WithCollidingName));
        var operation = MakeOperation("id", "name", "email");

        var context = new OperationFilterContext(null, null, null, null, methodInfo);
        new BindNeverOperationFilter().Apply(operation, context);

        var remaining = Assert.Single(operation.Parameters);
        Assert.Equal("name", remaining.Name);
    }

    [Fact]
    public void PreservesParameterWhenBindNeverComesAfterCollidingName()
    {
        var methodInfo = typeof(TestController).GetMethod(nameof(TestController.WithCollidingNameReversed));
        var operation = MakeOperation("id", "name", "email");

        var context = new OperationFilterContext(null, null, null, null, methodInfo);
        new BindNeverOperationFilter().Apply(operation, context);

        var remaining = Assert.Single(operation.Parameters);
        Assert.Equal("name", remaining.Name);
    }

    [Fact]
    public void DoesNotRemoveNonQueryParameters()
    {
        var methodInfo = typeof(TestController).GetMethod(nameof(TestController.WithFromBody));
        var operation = new OpenApiOperation
        {
            Parameters = new List<IOpenApiParameter>
            {
                new OpenApiParameter { Name = "id", In = ParameterLocation.Query },
                new OpenApiParameter { Name = "name", In = ParameterLocation.Header },
            }
        };

        var context = new OperationFilterContext(null, null, null, null, methodInfo);
        new BindNeverOperationFilter().Apply(operation, context);

        var remaining = Assert.Single(operation.Parameters);
        Assert.Equal("name", remaining.Name);
    }

    [Fact]
    public void HandlesNullParametersList()
    {
        var methodInfo = typeof(TestController).GetMethod(nameof(TestController.WithBindNeverComplexType));
        var operation = new OpenApiOperation();

        var context = new OperationFilterContext(null, null, null, null, methodInfo);
        new BindNeverOperationFilter().Apply(operation, context);

        Assert.Null(operation.Parameters);
    }
}
