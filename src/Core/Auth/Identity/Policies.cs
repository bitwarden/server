using Bit.Core.Auth.IdentityServer;

namespace Bit.Core.Auth.Identity;

public static class Policies
{
    /// <summary>
    /// Policy for managing access to the Send feature.
    /// </summary>
    /// <remarks>
    /// <example>
    /// Can be used with the <c>Authorize</c> attribute, for example:
    /// <code>
    /// [Authorize(Policy = Policies.Send)]
    /// </code>
    /// </example>
    /// </remarks>
    public const string Send = "Send";

    /// <summary>
    /// Policy to manage access to general API endpoints.
    /// </summary>
    /// <remarks>
    /// <example>
    /// Can be used with the <c>Authorize</c> attribute, for example:
    /// <code>
    /// [Authorize(Policy = Policies.Application)]
    /// </code>
    /// </example>
    /// </remarks>
    public const string Application = "Application";

    /// <summary>
    /// Policy to manage access to API endpoints intended for use by the Web Vault and browser extension only.
    /// </summary>
    /// <remarks>
    /// <example>
    /// Can be used with the <c>Authorize</c> attribute, for example:
    /// <code>
    /// [Authorize(Policy = Policies.Web)]
    /// </code>
    /// </example>
    /// </remarks>
    public const string Web = "Web";

    /// <summary>
    /// Policy to restrict access to API endpoints for the Push feature.
    /// </summary>
    /// <remarks>
    /// <example>
    /// Can be used with the <c>Authorize</c> attribute, for example:
    /// <code>
    /// [Authorize(Policy = Policies.Push)]
    /// </code>
    /// </example>
    /// </remarks>
    public const string Push = "Push";

    // TODO: This is unused
    public const string Licensing = "Licensing"; // [Authorize(Policy = Policies.Licensing)]

    /// <summary>
    /// Policy to restrict access to API endpoints related to the Organization features.
    /// </summary>
    /// <remarks>
    /// <example>
    /// Can be used with the <c>Authorize</c> attribute, for example:
    /// <code>
    /// [Authorize(Policy = Policies.Licensing)]
    /// </code>
    /// </example>
    /// </remarks>
    public const string Organization = "Organization";

    public const string OrganizationMembersRead = "OrganizationMembersRead";
    public const string OrganizationMembersWrite = "OrganizationMembersWrite";
    public const string OrganizationGroupsRead = "OrganizationGroupsRead";
    public const string OrganizationGroupsWrite = "OrganizationGroupsWrite";
    public const string OrganizationCollectionsRead = "OrganizationCollectionsRead";
    public const string OrganizationCollectionsWrite = "OrganizationCollectionsWrite";
    public const string OrganizationPoliciesRead = "OrganizationPoliciesRead";
    public const string OrganizationEventsRead = "OrganizationEventsRead";
    public const string OrganizationSubscriptionRead = "OrganizationSubscriptionRead";
    public const string OrganizationSubscriptionWrite = "OrganizationSubscriptionWrite";

    /// <summary>
    /// Maps each scoped organization policy to the scope it accepts in addition to <see cref="ApiScopes.ApiOrganization"/>.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string> OrganizationScopedPolicyScopes =
        new Dictionary<string, string>
        {
            [OrganizationMembersRead] = ApiScopes.ApiOrganizationMembersRead,
            [OrganizationMembersWrite] = ApiScopes.ApiOrganizationMembersWrite,
            [OrganizationGroupsRead] = ApiScopes.ApiOrganizationGroupsRead,
            [OrganizationGroupsWrite] = ApiScopes.ApiOrganizationGroupsWrite,
            [OrganizationCollectionsRead] = ApiScopes.ApiOrganizationCollectionsRead,
            [OrganizationCollectionsWrite] = ApiScopes.ApiOrganizationCollectionsWrite,
            [OrganizationPoliciesRead] = ApiScopes.ApiOrganizationPoliciesRead,
            [OrganizationEventsRead] = ApiScopes.ApiOrganizationEventsRead,
            [OrganizationSubscriptionRead] = ApiScopes.ApiOrganizationSubscriptionRead,
            [OrganizationSubscriptionWrite] = ApiScopes.ApiOrganizationSubscriptionWrite,
        };

    /// <summary>
    /// Policy to restrict access to API endpoints related to the setting up new installations.
    /// </summary>
    /// <remarks>
    /// <example>
    /// Can be used with the <c>Authorize</c> attribute, for example:
    /// <code>
    /// [Authorize(Policy = Policies.Installation)]
    /// </code>
    /// </example>
    /// </remarks>
    public const string Installation = "Installation";

    /// <summary>
    /// Policy to restrict access to API endpoints for Secrets Manager features.
    /// </summary>
    /// <remarks>
    /// <example>
    /// Can be used with the <c>Authorize</c> attribute, for example:
    /// <code>
    /// [Authorize(Policy = Policies.Secrets)]
    /// </code>
    /// </example>
    /// </remarks>
    public const string Secrets = "Secrets";
}
