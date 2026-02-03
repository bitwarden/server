using Bit.Infrastructure.EntityFramework.Models;

namespace Bit.Seeder.Factories;

/// <summary>
/// Creates organization domain entities for seeding.
/// </summary>
public static class OrganizationDomainSeeder
{
    /// <summary>
    /// Creates a verified organization domain entity.
    /// </summary>
    /// <param name="organizationId">The organization ID.</param>
    /// <param name="domainName">The domain name (e.g., "example.com").</param>
    /// <returns>A new verified OrganizationDomain entity (not persisted).</returns>
    public static OrganizationDomain CreateVerifiedDomain(Guid organizationId, string domainName)
    {
        var domain = new OrganizationDomain
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            DomainName = domainName,
            Txt = Guid.NewGuid().ToString("N"),
            CreationDate = DateTime.UtcNow,
        };

        domain.SetVerifiedDate();
        domain.SetLastCheckedDate();

        return domain;
    }
}
