using Bit.Core.Entities;
using Bit.Core.Utilities;

namespace Bit.Seeder.Factories;

internal static class OrganizationDomainSeeder
{
    internal static OrganizationDomain Create(Guid organizationId, string domainName)
    {
        var domain = new OrganizationDomain
        {
            Id = CoreHelpers.GenerateComb(),
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
