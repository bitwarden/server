namespace Bit.Infrastructure.EntityFramework.Repositories.Queries;

public class OrganizationUserReadManagedIdsByOrganizationIdQuery : IQuery<Guid>
{
    private readonly Guid _organizationId;

    public OrganizationUserReadManagedIdsByOrganizationIdQuery(Guid organizationId)
    {
        _organizationId = organizationId;
    }

    public IQueryable<Guid> Run(DatabaseContext dbContext)
    {
        var query = dbContext.OrganizationUsers
            .Where(ou => ou.OrganizationId == _organizationId &&
                         dbContext.OrganizationDomains
                             .Any(od => od.OrganizationId == _organizationId &&
                                        od.VerifiedDate != null &&
                                        od.DomainName == ou.Email.Substring(ou.Email.IndexOf('@') + 1)))
            .Select(ou => ou.Id);

        return query;
    }
}
