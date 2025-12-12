using Bit.Infrastructure.EntityFramework.Models;
using Bit.Infrastructure.EntityFramework.Repositories;

namespace Bit.Seeder.Recipes;

public class OrganizationDomainRecipe(DatabaseContext db)
{
    public void AddVerifiedDomainToOrganization(Guid organizationId, string domainName)
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

        db.Add(domain);
        db.SaveChanges();
    }
}
