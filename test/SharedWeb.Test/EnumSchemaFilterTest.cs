using Bit.SharedWeb.Swagger;
using Microsoft.OpenApi;
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
        var schema = new OpenApiSchema
        {
            Extensions = new Dictionary<string, IOpenApiExtension>()
        };
        var context = new SchemaFilterContext(typeof(TestEnum), null, null, null);
        var filter = new EnumSchemaFilter();
        filter.Apply(schema, context);

        Assert.True(schema.Extensions.ContainsKey("x-enum-varnames"));
        var extensions = (schema.Extensions["x-enum-varnames"] as JsonNodeExtension).Node.AsArray();
        Assert.NotNull(extensions);
        Assert.Equal(["First", "Second", "Third"], extensions.GetValues<string>().Select(x => x));
    }

    [Fact]
    public void DoesNotSetExtensionForNonEnum()
    {
        var schema = new OpenApiSchema
        {
            Extensions = new Dictionary<string, IOpenApiExtension>()
        };
        var context = new SchemaFilterContext(typeof(string), null, null, null);
        var filter = new EnumSchemaFilter();
        filter.Apply(schema, context);

        Assert.False(schema.Extensions.ContainsKey("x-enum-varnames"));
    }
}
