using Bit.Core.Entities;
using Bit.Core.Utilities;

namespace Bit.Seeder.Factories;

internal static class OrganizationDomainSeeder
{
    private const int VerificationIntervalHours = 12;

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
        domain.SetNextRunDate(12);

        return domain;
    }
}
