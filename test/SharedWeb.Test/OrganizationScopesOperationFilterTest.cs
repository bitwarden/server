using Bit.Core.Auth.Identity;
using Bit.Core.Auth.IdentityServer;
using Bit.SharedWeb.Swagger;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace SharedWeb.Test;

public class OrganizationScopesOperationFilterTest
{
    private const string UsScheme = "US_server";
    private const string EuScheme = "EU_server";

    [Fact]
    public void Apply_ScopedPolicy_AcceptsItsScopeOrLegacyScope()
    {
        var operation = ApplyFilter(new AuthorizeAttribute(Policies.OrganizationMembersRead));

        Assert.Equal(
            [
                $"{UsScheme}: {ApiScopes.ApiOrganizationMembersRead}",
                $"{UsScheme}: {ApiScopes.ApiOrganization}",
                $"{EuScheme}: {ApiScopes.ApiOrganizationMembersRead}",
                $"{EuScheme}: {ApiScopes.ApiOrganization}",
            ],
            Describe(operation));
    }

    [Fact]
    public void Apply_SeveralScopedPolicies_RequiresAllTheirScopesTogether()
    {
        var operation = ApplyFilter(
            new AuthorizeAttribute(Policies.OrganizationMembersWrite),
            new AuthorizeAttribute(Policies.OrganizationGroupsWrite));

        Assert.Contains(
            $"{UsScheme}: {ApiScopes.ApiOrganizationMembersWrite} {ApiScopes.ApiOrganizationGroupsWrite}",
            Describe(operation));
        Assert.Contains($"{UsScheme}: {ApiScopes.ApiOrganization}", Describe(operation));
    }

    [Fact]
    public void Apply_PolicyNameInDifferentCase_IsRecognized()
    {
        var operation = ApplyFilter(new AuthorizeAttribute(Policies.OrganizationEventsRead.ToLowerInvariant()));

        Assert.Contains($"{UsScheme}: {ApiScopes.ApiOrganizationEventsRead}", Describe(operation));
    }

    [Fact]
    public void Apply_LegacyOrganizationPolicy_AcceptsOnlyLegacyScope()
    {
        var operation = ApplyFilter(
            new AuthorizeAttribute(Policies.Organization),
            new AuthorizeAttribute(Policies.OrganizationPoliciesRead));

        Assert.Equal(
            [$"{UsScheme}: {ApiScopes.ApiOrganization}", $"{EuScheme}: {ApiScopes.ApiOrganization}"],
            Describe(operation));
    }

    [Fact]
    public void Apply_NoOrganizationPolicy_LeavesSecurityUnset()
    {
        var operation = ApplyFilter(new AuthorizeAttribute(Policies.Application));

        Assert.Null(operation.Security);
    }

    [Fact]
    public void Apply_AllowAnonymous_LeavesSecurityUnset()
    {
        var operation = ApplyFilter(new AuthorizeAttribute(Policies.OrganizationMembersRead), new AllowAnonymousAttribute());

        Assert.Null(operation.Security);
    }

    [Fact]
    public void GetClientCredentialsScopes_ListsLegacyAndEveryCatalogScope()
    {
        var scopes = OrganizationScopesOperationFilter.GetClientCredentialsScopes("Organization APIs");

        Assert.Equal("Organization APIs", scopes[ApiScopes.ApiOrganization]);
        Assert.Equivalent(ApiScopes.OrganizationApiKeyScopes.Append(ApiScopes.ApiOrganization), scopes.Keys, strict: true);
        Assert.Equal("Read Organization Members", scopes[ApiScopes.ApiOrganizationMembersRead]);
    }

    [Fact]
    public void RebindOperationRequirements_PointsRequirementsAtOneSchemeWithoutDuplicates()
    {
        var document = CreateDocument();
        var operation = ApplyFilter(document, new AuthorizeAttribute(Policies.OrganizationMembersRead));
        AddOperation(document, "/public/members", operation);

        OrganizationScopesOperationFilter.RebindOperationRequirements(document, "oauth2-client-credentials");

        Assert.Equal(
            [
                $"oauth2-client-credentials: {ApiScopes.ApiOrganizationMembersRead}",
                $"oauth2-client-credentials: {ApiScopes.ApiOrganization}",
            ],
            Describe(operation));
    }

    [Fact]
    public void RebindOperationRequirements_LeavesNonOAuth2RequirementsUntouched()
    {
        var document = CreateDocument();
        document.Components!.SecuritySchemes!["send-access-bearer"] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
        };
        var operation = new OpenApiOperation
        {
            Security = [new OpenApiSecurityRequirement { [new OpenApiSecuritySchemeReference("send-access-bearer", document)] = [] }],
        };
        AddOperation(document, "/sends/access", operation);

        OrganizationScopesOperationFilter.RebindOperationRequirements(document, "oauth2-client-credentials");

        Assert.Equal(["send-access-bearer: "], Describe(operation));
    }

    private static OpenApiOperation ApplyFilter(params object[] endpointMetadata) =>
        ApplyFilter(CreateDocument(), endpointMetadata);

    private static OpenApiOperation ApplyFilter(OpenApiDocument document, params object[] endpointMetadata)
    {
        var options = new SwaggerGenOptions();
        foreach (var (schemeId, scheme) in document.Components!.SecuritySchemes!)
        {
            options.AddSecurityDefinition(schemeId, (OpenApiSecurityScheme)scheme);
        }

        var apiDescription = new ApiDescription
        {
            ActionDescriptor = new ActionDescriptor { EndpointMetadata = endpointMetadata.ToList() },
        };
        var context = new OperationFilterContext(apiDescription, null, null, document, null);
        var operation = new OpenApiOperation();

        new OrganizationScopesOperationFilter(Options.Create(options)).Apply(operation, context);

        return operation;
    }

    private static OpenApiDocument CreateDocument() => new()
    {
        Paths = new OpenApiPaths(),
        Components = new OpenApiComponents
        {
            SecuritySchemes = new Dictionary<string, IOpenApiSecurityScheme>
            {
                [UsScheme] = ClientCredentialsScheme("https://identity.example.com/connect/token"),
                [EuScheme] = ClientCredentialsScheme("https://identity.example.eu/connect/token"),
            },
        },
    };

    private static OpenApiSecurityScheme ClientCredentialsScheme(string tokenUrl) => new()
    {
        Type = SecuritySchemeType.OAuth2,
        Flows = new OpenApiOAuthFlows
        {
            ClientCredentials = new OpenApiOAuthFlow
            {
                TokenUrl = new Uri(tokenUrl),
                Scopes = OrganizationScopesOperationFilter.GetClientCredentialsScopes("Organization APIs"),
            },
        },
    };

    private static void AddOperation(OpenApiDocument document, string path, OpenApiOperation operation) =>
        document.Paths[path] = new OpenApiPathItem
        {
            Operations = new Dictionary<HttpMethod, OpenApiOperation> { [HttpMethod.Get] = operation },
        };

    private static List<string> Describe(OpenApiOperation operation) =>
        (operation.Security ?? [])
            .SelectMany(r => r.Select(entry => $"{entry.Key.Reference.Id}: {string.Join(' ', entry.Value)}"))
            .ToList();
}
