using Bit.Api.IntegrationTest.Factories;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.Swagger;
using Xunit;

namespace Bit.Api.IntegrationTest.AdminConsole.Partnerships;

public class PartnershipOpenApiDocumentTests : IClassFixture<ApiApplicationFactory>
{
    private static readonly string[] _partnershipOperations =
    [
        "GET /partnerships/v1/entitlements",
        "GET /partnerships/v1/entitlements/{externalId}",
        "GET /partnerships/v1/partnership",
        "GET /partnerships/v1/reconciliation-exports/{id}",
        "POST /partnerships/v1/activation-links",
        "POST /partnerships/v1/entitlements",
        "POST /partnerships/v1/entitlements/{externalId}/cancel",
        "POST /partnerships/v1/entitlements/{externalId}/resume",
        "POST /partnerships/v1/entitlements/{externalId}/suspend",
        "POST /partnerships/v1/reconciliation-exports",
    ];

    private readonly ISwaggerProvider _swaggerProvider;

    public PartnershipOpenApiDocumentTests(ApiApplicationFactory factory)
    {
        _swaggerProvider = factory.GetService<ISwaggerProvider>();
    }

    [Fact]
    public void PartnershipsDocument_ListsEveryPartnershipOperation()
    {
        var document = _swaggerProvider.GetSwagger("partnerships");

        Assert.Equal(_partnershipOperations,
            Operations(document).Where(o => o.Contains(" /partnerships/", StringComparison.Ordinal))
                .Order(StringComparer.Ordinal));
    }

    [Theory]
    [InlineData("internal")]
    [InlineData("public")]
    public void OtherDocuments_ContainNoPartnershipPaths(string documentName)
    {
        var document = _swaggerProvider.GetSwagger(documentName);

        Assert.DoesNotContain(document.Paths.Keys, path => path.StartsWith("/partnerships/", StringComparison.Ordinal));
    }

    [Fact]
    public void PartnershipsDocument_SchemasCarryNoUserId()
    {
        var document = _swaggerProvider.GetSwagger("partnerships");

        var propertyNames = document.Components!.Schemas!.Values
            .SelectMany(schema => schema.Properties?.Keys ?? []);
        Assert.DoesNotContain(propertyNames, name => name.Equals("userId", StringComparison.OrdinalIgnoreCase));
    }

    private static IEnumerable<string> Operations(OpenApiDocument document) =>
        document.Paths.SelectMany(path => (path.Value.Operations ?? []).Keys
            .Select(method => $"{method.Method.ToUpperInvariant()} {path.Key}"));
}
