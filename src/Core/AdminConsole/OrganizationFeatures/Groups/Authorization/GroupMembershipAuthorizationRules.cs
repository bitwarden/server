namespace Bit.Core.AdminConsole.OrganizationFeatures.Groups.Authorization;

/// <summary>
/// Authorization rules for group membership changes.
/// </summary>
public static class GroupMembershipAuthorizationRules
{
    public static bool CanAddSelfToGroups(bool allowAdminAccessToAllCollectionItems) =>
        allowAdminAccessToAllCollectionItems;
}
