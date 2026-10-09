using Duende.IdentityServer.Models;

namespace Bit.Core.Auth.IdentityServer;

public static class ApiScopes
{
    public const string Api = "api";
    public const string ApiInstallation = "api.installation";
    public const string ApiLicensing = "api.licensing";
    public const string ApiOrganization = "api.organization";
    public const string ApiPush = "api.push";
    public const string ApiSecrets = "api.secrets";
    public const string Internal = "internal";
    public const string ApiSendAccess = "api.send.access";

    public const string ApiOrganizationMembersRead = "api.organization.members.read";
    public const string ApiOrganizationMembersWrite = "api.organization.members.write";
    public const string ApiOrganizationGroupsRead = "api.organization.groups.read";
    public const string ApiOrganizationGroupsWrite = "api.organization.groups.write";
    public const string ApiOrganizationCollectionsRead = "api.organization.collections.read";
    public const string ApiOrganizationCollectionsWrite = "api.organization.collections.write";
    public const string ApiOrganizationPoliciesRead = "api.organization.policies.read";
    public const string ApiOrganizationEventsRead = "api.organization.events.read";
    public const string ApiOrganizationSubscriptionRead = "api.organization.subscription.read";
    public const string ApiOrganizationSubscriptionWrite = "api.organization.subscription.write";

    /// <summary>
    /// Scopes that can be granted to a scoped organization API key. Does not include <see cref="ApiOrganization"/>.
    /// </summary>
    public static readonly IReadOnlyList<string> OrganizationApiKeyScopes =
    [
        ApiOrganizationMembersRead,
        ApiOrganizationMembersWrite,
        ApiOrganizationGroupsRead,
        ApiOrganizationGroupsWrite,
        ApiOrganizationCollectionsRead,
        ApiOrganizationCollectionsWrite,
        ApiOrganizationPoliciesRead,
        ApiOrganizationEventsRead,
        ApiOrganizationSubscriptionRead,
        ApiOrganizationSubscriptionWrite,
    ];

    public static IEnumerable<ApiScope> GetApiScopes()
    {
        return new List<ApiScope>
        {
            new(Api, "API Access"),
            new(ApiPush, "API Push Access"),
            new(ApiLicensing, "API Licensing Access"),
            new(ApiOrganization, "API Organization Access"),
            new(ApiInstallation, "API Installation Access"),
            new(Internal, "Internal Access"),
            new(ApiSecrets, "Secrets Manager Access"),
            new(ApiSendAccess, "API Send Access"),
            new(ApiOrganizationMembersRead, "Read Organization Members"),
            new(ApiOrganizationMembersWrite, "Manage Organization Members"),
            new(ApiOrganizationGroupsRead, "Read Organization Groups"),
            new(ApiOrganizationGroupsWrite, "Manage Organization Groups"),
            new(ApiOrganizationCollectionsRead, "Read Organization Collections"),
            new(ApiOrganizationCollectionsWrite, "Manage Organization Collections"),
            new(ApiOrganizationPoliciesRead, "Read Organization Policies"),
            new(ApiOrganizationEventsRead, "Read Organization Events"),
            new(ApiOrganizationSubscriptionRead, "Read Organization Subscription"),
            new(ApiOrganizationSubscriptionWrite, "Manage Organization Subscription"),
        };
    }
}
