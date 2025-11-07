using Bit.Core.Models.Data.Organizations.OrganizationUsers;
using Bit.Core.Repositories;
using Microsoft.Extensions.Caching.Memory;

namespace Bit.Core.Services;

public class OrganizationUserUserDetailsCache(
    IMemoryCache memoryCache,
    TimeSpan cacheEntryTtl,
    IOrganizationUserRepository userRepository) :
        IntegrationTemplatePropertyCache<OrganizationUserKey, OrganizationUserUserDetails>(
            memoryCache,
            cacheEntryTtl),
        IOrganizationUserUserDetailsCache
{
    protected override Task<OrganizationUserUserDetails?> LoadValueFromRepositoryAsync(OrganizationUserKey key)
    {
        return userRepository.GetDetailsByOrganizationIdUserIdAsync(organizationId: key.OrganizationId, userId: key.UserId);
    }

    public async Task<OrganizationUserUserDetails?> GetAsync(Guid organizationId, Guid userId)
    {
        return await GetAsync(new OrganizationUserKey(organizationId, userId));
    }
}

public readonly struct OrganizationUserKey : IEquatable<OrganizationUserKey>
{
    public Guid OrganizationId { get; }
    public Guid UserId { get; }

    public OrganizationUserKey(Guid organizationId, Guid userId)
    {
        OrganizationId = organizationId;
        UserId = userId;
    }

    public bool Equals(OrganizationUserKey other) =>
        OrganizationId.Equals(other.OrganizationId) && UserId.Equals(other.UserId);

    public override bool Equals(object? obj) => obj is OrganizationUserKey other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(OrganizationId, UserId);
}
