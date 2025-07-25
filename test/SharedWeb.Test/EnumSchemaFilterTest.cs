using Bit.SharedWeb.Swagger;
using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace SharedWeb.Test;

public class EnumSchemaFilterTest
{
    private enum TestEnum
    {
        First,
        Second,
        Third
    }

    [Fact]
    public void SetsEnumVarNamesExtension()
    {
        var schema = new OpenApiSchema();
        var context = new SchemaFilterContext(typeof(TestEnum), null, null, null);
        var filter = new EnumSchemaFilter();
        filter.Apply(schema, context);

        Assert.True(schema.Extensions.ContainsKey("x-enum-varnames"));
        var extensions = schema.Extensions["x-enum-varnames"] as OpenApiArray;
        Assert.NotNull(extensions);
        Assert.Equal(["First", "Second", "Third"], extensions.Select(x => ((OpenApiString)x).Value));
    }

    [Fact]
    public void DoesNotSetExtensionForNonEnum()
    {
        var schema = new OpenApiSchema();
        var context = new SchemaFilterContext(typeof(string), null, null, null);
        var filter = new EnumSchemaFilter();
        filter.Apply(schema, context);

        Assert.False(schema.Extensions.ContainsKey("x-enum-varnames"));
    }
}
