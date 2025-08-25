using Bit.Core.Utilities;
using Bit.SharedWeb.Swagger;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;


namespace SharedWeb.Test;

public class EncryptedStringSchemaFilterTest
{
    private class TestClass
    {
        [EncryptedString]
        public string SecretKey { get; set; }

        public string Username { get; set; }

        [EncryptedString]
        public int Wrong { get; set; }
    }

    [Fact]
    public void AnnotatedStringSetsFormat()
    {
        var schema = new OpenApiSchema
        {
            Properties = new Dictionary<string, OpenApiSchema> { { "secretKey", new() } }
        };
        var context = new SchemaFilterContext(typeof(TestClass), null, null, null);
        var filter = new EncryptedStringSchemaFilter();
        filter.Apply(schema, context);
        Assert.Equal("x-enc-string", schema.Properties["secretKey"].Format);
    }

    [Fact]
    public void NonAnnotatedStringIsIgnored()
    {
        var schema = new OpenApiSchema
        {
            Properties = new Dictionary<string, OpenApiSchema> { { "username", new() } }
        };
        var context = new SchemaFilterContext(typeof(TestClass), null, null, null);
        var filter = new EncryptedStringSchemaFilter();
        filter.Apply(schema, context);
        Assert.Null(schema.Properties["username"].Format);
    }

    [Fact]
    public void AnnotatedWrongTypeIsIgnored()
    {
        var schema = new OpenApiSchema
        {
            Properties = new Dictionary<string, OpenApiSchema> { { "wrong", new() } }
        };
        var context = new SchemaFilterContext(typeof(TestClass), null, null, null);
        var filter = new EncryptedStringSchemaFilter();
        filter.Apply(schema, context);
        Assert.Null(schema.Properties["wrong"].Format);
    }
}
