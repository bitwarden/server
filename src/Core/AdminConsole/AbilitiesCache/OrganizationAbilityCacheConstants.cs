namespace Bit.Core.AdminConsole.AbilitiesCache;

public static class OrganizationAbilityCacheConstants
{
    public const string CacheName = "OrganizationAbilities";

    public static string BuildCacheKeyForOrganizationAbility(Guid organizationId)
        => $"org-ability:{organizationId:N}";
}
