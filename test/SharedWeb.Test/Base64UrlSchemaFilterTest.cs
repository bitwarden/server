using System.Reflection;
using System.Text.Json.Serialization;
using Bit.SharedWeb.Swagger;
using Fido2NetLib;
using Fido2NetLib.Objects;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace SharedWeb.Test;

public class Base64UrlSchemaFilterTest
{
    private const string Extension = "x-base64url";

    /// <summary>
    /// Every schema type the filter annotates, paired with the JSON property names it marks.
    /// A type absent from this table must come out of the filter untouched.
    /// </summary>
    public static TheoryData<Type, string[]> MarkedProperties => new()
    {
        { typeof(AssertionOptions), ["challenge"] },
        { typeof(CredentialCreateOptions), ["challenge"] },
        { typeof(Fido2User), ["id"] },
        { typeof(PublicKeyCredentialDescriptor), ["id"] },
        { typeof(AuthenticatorAttestationRawResponse), ["rawId"] },
        {
            typeof(AuthenticatorAttestationRawResponse.AttestationResponse),
            ["attestationObject", "clientDataJSON"]
        },
        { typeof(AuthenticatorAssertionRawResponse), ["rawId"] },
        {
            typeof(AuthenticatorAssertionRawResponse.AssertionResponse),
            ["authenticatorData", "signature", "clientDataJSON", "userHandle"]
        },
        { typeof(AuthenticationExtensionsPRFValues), ["first", "second"] },
        { typeof(AuthenticationExtensionsLargeBlobInputs), ["write"] },
        { typeof(AuthenticationExtensionsLargeBlobOutputs), ["blob"] },
    };

    private class UnmappedClass
    {
        public string Name { get; set; }
    }

    [Theory]
    [MemberData(nameof(MarkedProperties))]
    public void MarksMappedPropertiesAndLeavesSiblingsAlone(Type type, string[] properties)
    {
        var schema = SchemaWith([.. properties, "unrelated"]);

        Apply(type, schema);

        Assert.Equal([.. properties], MarkedPropertyNames(schema));
        Assert.All(properties, property => Assert.True(ExtensionValue(schema, property)));
    }

    /// <summary>
    /// Anchors the expected names to the Fido2 assembly. The filter names properties as string
    /// literals, so it cannot fail on a name the serialized model no longer has; reading the names
    /// back off the type turns an upstream rename, a typo, or a newly converted property into a
    /// test failure rather than a spec that describes the wrong encoding.
    /// </summary>
    [Theory]
    [MemberData(nameof(MarkedProperties))]
    public void ExpectedPropertiesMatchTheTypesBase64UrlConvertedProperties(Type type, string[] properties)
    {
        var converted = Base64UrlConvertedPropertyNames(type);

        Assert.NotEmpty(converted);
        Assert.Equal(converted, [.. properties]);
    }

    [Fact]
    public void IgnoresUnmappedType()
    {
        var schema = SchemaWith("challenge", "id");

        Apply(typeof(UnmappedClass), schema);

        Assert.Empty(MarkedPropertyNames(schema));
    }

    /// <summary>
    /// The filter marks by name rather than by reading the generated schema, so it adds a property
    /// the schema does not already carry.
    /// </summary>
    [Fact]
    public void AddsMappedPropertyMissingFromSchema()
    {
        var schema = SchemaWith("unrelated");

        Apply(typeof(AssertionOptions), schema);

        Assert.Equal(["challenge"], MarkedPropertyNames(schema));
    }

    [Fact]
    public void PopulatesSchemaWithNoProperties()
    {
        var schema = new OpenApiSchema();

        Apply(typeof(AssertionOptions), schema);

        Assert.Equal(["challenge"], MarkedPropertyNames(schema));
    }

    [Fact]
    public void KeepsExtensionsAlreadyOnTheProperty()
    {
        var schema = SchemaWith("challenge");
        ((OpenApiSchema)schema.Properties["challenge"]).Extensions =
            new Dictionary<string, IOpenApiExtension> { ["x-other"] = new JsonNodeExtension(false) };

        Apply(typeof(AssertionOptions), schema);

        Assert.Equal(["x-other", Extension], schema.Properties["challenge"].Extensions.Keys);
    }

    private static OpenApiSchema SchemaWith(params string[] properties) => new()
    {
        Properties = properties.ToDictionary(name => name, _ => (IOpenApiSchema)new OpenApiSchema())
    };

    private static void Apply(Type type, OpenApiSchema schema) =>
        new Base64UrlSchemaFilter().Apply(schema, new SchemaFilterContext(type, null, null, null));

    private static SortedSet<string> MarkedPropertyNames(OpenApiSchema schema) =>
        [.. schema.Properties
            .Where(property => property.Value.Extensions?.ContainsKey(Extension) == true)
            .Select(property => property.Key)];

    private static bool ExtensionValue(OpenApiSchema schema, string property) =>
        ((JsonNodeExtension)schema.Properties[property].Extensions[Extension]).Node.GetValue<bool>();

    private static SortedSet<string> Base64UrlConvertedPropertyNames(Type type) =>
        [.. type
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(property =>
                property.GetCustomAttribute<JsonConverterAttribute>()?.ConverterType
                    == typeof(Base64UrlConverter))
            .Select(property =>
                property.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name
                    ?? CamelCase(property.Name))];

    private static string CamelCase(string name) => char.ToLowerInvariant(name[0]) + name[1..];
}
