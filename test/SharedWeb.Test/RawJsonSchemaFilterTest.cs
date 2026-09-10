using Bit.Core.Utilities;
using Bit.SharedWeb.Swagger;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace SharedWeb.Test;

public class RawJsonSchemaFilterTest
{
    private class TestClass
    {
        [System.Text.Json.Serialization.JsonConverter(typeof(RawJsonConverter))]
        public string Data { get; set; }

        public string Username { get; set; }

        [System.Text.Json.Serialization.JsonConverter(typeof(RawJsonConverter))]
        public int Wrong { get; set; }
    }

    [Fact]
    public void RawJsonConverterPropertyBecomesNullableObjectSchema()
    {
        var schema = new OpenApiSchema
        {
            Properties = new Dictionary<string, IOpenApiSchema>
            {
                { "data", new OpenApiSchema { Type = JsonSchemaType.String | JsonSchemaType.Null } },
            },
        };
        var context = new SchemaFilterContext(typeof(TestClass), null, null, null);
        var filter = new RawJsonSchemaFilter();
        filter.Apply(schema, context);
        Assert.Equal(JsonSchemaType.Object | JsonSchemaType.Null, schema.Properties["data"].Type);
    }

    [Fact]
    public void RawJsonConverterPropertyWithoutNullFlagBecomesObjectSchema()
    {
        var schema = new OpenApiSchema
        {
            Properties = new Dictionary<string, IOpenApiSchema>
            {
                { "data", new OpenApiSchema { Type = JsonSchemaType.String } },
            },
        };
        var context = new SchemaFilterContext(typeof(TestClass), null, null, null);
        var filter = new RawJsonSchemaFilter();
        filter.Apply(schema, context);
        Assert.Equal(JsonSchemaType.Object, schema.Properties["data"].Type);
    }

    [Fact]
    public void NonAnnotatedStringIsIgnored()
    {
        var schema = new OpenApiSchema
        {
            Properties = new Dictionary<string, IOpenApiSchema>
            {
                { "username", new OpenApiSchema { Type = JsonSchemaType.String } },
            },
        };
        var context = new SchemaFilterContext(typeof(TestClass), null, null, null);
        var filter = new RawJsonSchemaFilter();
        filter.Apply(schema, context);
        Assert.Equal(JsonSchemaType.String, schema.Properties["username"].Type);
    }

    [Fact]
    public void AnnotatedWrongTypeIsIgnored()
    {
        var schema = new OpenApiSchema
        {
            Properties = new Dictionary<string, IOpenApiSchema>
            {
                { "wrong", new OpenApiSchema { Type = JsonSchemaType.Integer } },
            },
        };
        var context = new SchemaFilterContext(typeof(TestClass), null, null, null);
        var filter = new RawJsonSchemaFilter();
        filter.Apply(schema, context);
        Assert.Equal(JsonSchemaType.Integer, schema.Properties["wrong"].Type);
    }
}
