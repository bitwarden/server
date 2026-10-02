using Bit.Core.Entities;
using Bit.Core.Utilities;

namespace Bit.Seeder.Factories;

internal static class OrganizationDomainSeeder
{
    internal static OrganizationDomain Create(Guid organizationId, string domainName)
    {
        var domain = new OrganizationDomain
        {
            Id = CombGuid.Generate(),
            OrganizationId = organizationId,
            DomainName = domainName,
            Txt = Guid.NewGuid().ToString("N"),
            CreationDate = DateTime.UtcNow,
        };

        domain.SetVerifiedDate();
        domain.SetLastCheckedDate();
        // Without this NextRunDate stays at 0001-01-01 and overflows SQL Server's datetime range on insert.
        domain.SetNextRunDate(12);

        return domain;
    }
}
