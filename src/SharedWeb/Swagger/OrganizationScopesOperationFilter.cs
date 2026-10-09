using Bit.Core.Auth.Identity;
using Bit.Core.Auth.IdentityServer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Bit.SharedWeb.Swagger;

/// <summary>
/// Sets each operation's OAuth2 client credentials security requirements to the scopes its organization
/// authorization policies accept: one requirement with every scoped policy's scope (all required), and one
/// with <see cref="ApiScopes.ApiOrganization"/>, which every organization policy accepts.
/// </summary>
public class OrganizationScopesOperationFilter(IOptions<SwaggerGenOptions> swaggerGenOptions) : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var scopeSets = GetAcceptedScopeSets(context.ApiDescription.ActionDescriptor.EndpointMetadata);
        if (scopeSets.Count == 0)
        {
            return;
        }

        // The document's schemes are populated after operation filters run, so read them from the options.
        var schemeIds = swaggerGenOptions.Value.SwaggerGeneratorOptions.SecuritySchemes
            .Where(s => s.Value.Type == SecuritySchemeType.OAuth2 && s.Value.Flows?.ClientCredentials != null)
            .Select(s => s.Key)
            .ToList();
        if (schemeIds.Count == 0)
        {
            return;
        }

        operation.Security = schemeIds
            .SelectMany(schemeId => scopeSets.Select(scopes => new OpenApiSecurityRequirement
            {
                [new OpenApiSecuritySchemeReference(schemeId, context.Document)] = scopes.ToList()
            }))
            .ToList();
    }

    /// <summary>
    /// Returns the alternative scope sets that satisfy every authorization policy in <paramref name="endpointMetadata"/>,
    /// or none when the endpoint has no organization policy.
    /// </summary>
    private static IReadOnlyList<IReadOnlyList<string>> GetAcceptedScopeSets(IEnumerable<object> endpointMetadata)
    {
        var metadata = endpointMetadata.ToList();
        if (metadata.OfType<IAllowAnonymous>().Any())
        {
            return [];
        }

        var policies = metadata.OfType<IAuthorizeData>()
            .Select(a => a.Policy)
            .OfType<string>()
            .ToList();

        if (policies.Contains(Policies.Organization, StringComparer.OrdinalIgnoreCase))
        {
            return [[ApiScopes.ApiOrganization]];
        }

        var scopes = Policies.OrganizationScopedPolicyScopes
            .Where(p => policies.Contains(p.Key, StringComparer.OrdinalIgnoreCase))
            .Select(p => p.Value)
            .ToList();

        return scopes.Count == 0 ? [] : [scopes, [ApiScopes.ApiOrganization]];
    }

    /// <summary>
    /// The scopes to list on an OAuth2 client credentials scheme for organization API keys.
    /// </summary>
    public static Dictionary<string, string> GetClientCredentialsScopes(string apiOrganizationDescription)
    {
        var displayNames = ApiScopes.GetApiScopes().ToDictionary(s => s.Name, s => s.DisplayName ?? s.Name);
        var scopes = new Dictionary<string, string> { { ApiScopes.ApiOrganization, apiOrganizationDescription } };
        foreach (var scope in ApiScopes.OrganizationApiKeyScopes)
        {
            scopes.Add(scope, displayNames[scope]);
        }

        return scopes;
    }

    /// <summary>
    /// Points every operation requirement that references an OAuth2 scheme at <paramref name="schemeId"/> instead,
    /// dropping requirements that become duplicates. Call before replacing the document's security schemes.
    /// </summary>
    public static void RebindOperationRequirements(OpenApiDocument document, string schemeId)
    {
        var oauth2SchemeIds = document.Components?.SecuritySchemes?
            .Where(s => s.Value.Type == SecuritySchemeType.OAuth2)
            .Select(s => s.Key)
            .ToHashSet() ?? [];

        foreach (var operation in document.Paths.Values.SelectMany(p => p.Operations?.Values.AsEnumerable() ?? []))
        {
            if (operation.Security is not { Count: > 0 } ||
                !operation.Security.All(r => r.Keys.All(k => k.Reference.Id is not null && oauth2SchemeIds.Contains(k.Reference.Id))))
            {
                continue;
            }

            operation.Security = operation.Security
                .SelectMany(r => r.Values)
                .DistinctBy(scopes => string.Join(' ', scopes))
                .Select(scopes => new OpenApiSecurityRequirement
                {
                    [new OpenApiSecuritySchemeReference(schemeId, document)] = scopes
                })
                .ToList();
        }
    }
}
