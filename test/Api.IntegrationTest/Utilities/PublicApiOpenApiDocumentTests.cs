using System.Text.Json;
using Bit.Api.IntegrationTest.Factories;
using Bit.Core.Auth.Identity;
using Bit.Core.Auth.IdentityServer;
using Bit.SharedWeb.Swagger;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.Swagger;
using Xunit;

namespace Bit.Api.IntegrationTest.Utilities;

public class PublicApiOpenApiDocumentTests : IClassFixture<ApiApplicationFactory>
{
    private readonly ApiApplicationFactory _factory;

    public PublicApiOpenApiDocumentTests(ApiApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public void PublicDocument_ClientCredentialsSchemes_ListEveryOrganizationScope()
    {
        var document = GetDocument("public");

        var schemes = ClientCredentialsSchemes(document);

        Assert.NotEmpty(schemes);
        foreach (var scheme in schemes.Values)
        {
            var scopes = scheme.Flows!.ClientCredentials!.Scopes!.Keys;
            Assert.Equivalent(ApiScopes.OrganizationApiKeyScopes.Append(ApiScopes.ApiOrganization), scopes, strict: true);
        }
    }

    [Fact]
    public void PublicDocument_EachOperation_ListsTheScopesItsPoliciesAccept()
    {
        var document = GetDocument("public");
        var schemeIds = ClientCredentialsSchemes(document).Keys;
        var operations = document.Paths.Values
            .SelectMany(p => p.Operations!.Values)
            .ToDictionary(o => o.OperationId!);
        var apiDescriptions = _factory.GetService<IApiDescriptionGroupCollectionProvider>().ApiDescriptionGroups.Items
            .Where(g => g.GroupName == "public")
            .SelectMany(g => g.Items)
            .ToList();

        Assert.NotEmpty(apiDescriptions);
        foreach (var apiDescription in apiDescriptions)
        {
            var operation = operations[SwaggerGenOptionsExt.BuildOperationId(apiDescription)];
            var expected = ExpectedScopeSets(apiDescription)
                .SelectMany(scopes => schemeIds.Select(schemeId => Describe(schemeId, scopes)))
                .ToHashSet();
            var actual = (operation.Security ?? [])
                .SelectMany(r => r.Select(entry => Describe(entry.Key.Reference.Id, entry.Value)))
                .ToHashSet();

            Assert.NotEmpty(expected);
            Assert.True(expected.SetEquals(actual),
                $"{operation.OperationId}: expected [{string.Join(", ", expected)}], got [{string.Join(", ", actual)}]");
        }
    }

    [Fact]
    public async Task ServedPublicDocument_Operations_ReferenceTheLocalSchemeOnce()
    {
        var generated = GetDocument("public").Paths.Values
            .SelectMany(p => p.Operations!.Values)
            .ToDictionary(
                o => o.OperationId!,
                o => (o.Security ?? []).SelectMany(r => r.Values).Select(scopes => Describe("oauth2-client-credentials", scopes)).ToHashSet());

        using var client = _factory.CreateClient();
        using var served = JsonDocument.Parse(await client.GetStringAsync("specs/public/swagger.json"));

        var operations = served.RootElement.GetProperty("paths").EnumerateObject()
            .SelectMany(p => p.Value.EnumerateObject())
            .Where(o => o.Value.TryGetProperty("operationId", out _))
            .Select(o => o.Value)
            .ToList();
        Assert.NotEmpty(operations);
        foreach (var operation in operations)
        {
            var operationId = operation.GetProperty("operationId").GetString()!;
            var actual = operation.TryGetProperty("security", out var security)
                ? security.EnumerateArray()
                    .SelectMany(r => r.EnumerateObject())
                    .Select(entry => Describe(entry.Name, entry.Value.EnumerateArray().Select(s => s.GetString()!)))
                    .ToList()
                : [];

            Assert.Equal(generated[operationId].Count, actual.Count);
            Assert.True(generated[operationId].SetEquals(actual), operationId);
        }
    }

    [Fact]
    public void InternalDocument_Operations_HaveNoClientCredentialsRequirements()
    {
        var document = GetDocument("internal");
        var schemeIds = ClientCredentialsSchemes(document).Keys.ToHashSet();

        var operationSchemeIds = document.Paths.Values
            .SelectMany(p => p.Operations!.Values)
            .SelectMany(o => o.Security ?? [])
            .SelectMany(r => r.Keys)
            .Select(k => k.Reference.Id);

        Assert.DoesNotContain(operationSchemeIds, schemeIds.Contains);
        Assert.Equal(schemeIds.Count, document.Security!.Count);
        Assert.All(document.Security, r => Assert.Equal([ApiScopes.ApiOrganization], Assert.Single(r).Value));
    }

    private static string Describe(string? schemeId, IEnumerable<string> scopes) =>
        $"{schemeId}: {string.Join(' ', scopes.Order())}";

    private OpenApiDocument GetDocument(string documentName) =>
        _factory.GetService<ISwaggerProvider>().GetSwagger(documentName);

    private static Dictionary<string, IOpenApiSecurityScheme> ClientCredentialsSchemes(OpenApiDocument document) =>
        document.Components!.SecuritySchemes!
            .Where(s => s.Value.Type == SecuritySchemeType.OAuth2 && s.Value.Flows?.ClientCredentials != null)
            .ToDictionary();

    // A scoped token must hold the scope of every scoped policy on the action; api.organization satisfies them all.
    private static List<List<string>> ExpectedScopeSets(ApiDescription apiDescription)
    {
        var policies = apiDescription.ActionDescriptor.EndpointMetadata
            .OfType<IAuthorizeData>()
            .Select(a => a.Policy)
            .OfType<string>()
            .ToList();

        if (policies.Contains(Policies.Organization))
        {
            return [[ApiScopes.ApiOrganization]];
        }

        var scopes = policies
            .Where(Policies.OrganizationScopedPolicyScopes.ContainsKey)
            .Select(p => Policies.OrganizationScopedPolicyScopes[p])
            .Distinct()
            .ToList();
        return scopes.Count == 0 ? [] : [scopes, [ApiScopes.ApiOrganization]];
    }
}
