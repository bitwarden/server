using Bit.Api.IntegrationTest.Factories;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.Swagger;
using Xunit;

namespace Bit.Api.IntegrationTest.Platform;

public class OpenApiSpecTests
{
    /// <summary>
    /// A query parameter typed as an object model comes from an action parameter the server binds
    /// some other way, such as from route values. Generated clients would expose a parameter the
    /// server ignores.
    /// </summary>
    [Theory]
    [InlineData("internal")]
    [InlineData("public")]
    public async Task QueryParameters_AreNotObjectModels(string documentName)
    {
        await using var factory = new ApiApplicationFactory();
        var document = factory.GetService<ISwaggerProvider>().GetSwagger(documentName);

        var objectQueryParameters = (
            from path in document.Paths
            from operation in path.Value.Operations ?? []
            from parameter in operation.Value.Parameters ?? []
            where parameter.In == ParameterLocation.Query && IsObjectModel(document, parameter.Schema)
            select $"{operation.Key} {path.Key} {parameter.Name}").ToList();

        Assert.Empty(objectQueryParameters);
    }

    private static bool IsObjectModel(OpenApiDocument document, IOpenApiSchema? schema)
    {
        if (schema is not OpenApiSchemaReference reference
            || document.Components?.Schemas is not { } schemas
            || !schemas.TryGetValue(reference.Reference.Id!, out var target))
        {
            return false;
        }

        return target.Enum is not { Count: > 0 };
    }
}
