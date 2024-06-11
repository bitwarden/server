using Bit.Infrastructure.EntityFramework.Models;

namespace Bit.Infrastructure.EntityFramework.Repositories.Queries;

public class OrganizationUserReadCountByOrganizationIdEmailQuery : IQuery<OrganizationUser>
{
    private readonly Guid _organizationId;
    private readonly string _email;
    private readonly bool _onlyUsers;

    public OrganizationUserReadCountByOrganizationIdEmailQuery(Guid organizationId, string email, bool onlyUsers)
    {
        _organizationId = organizationId;
        _email = email;
        _onlyUsers = onlyUsers;
    }

    public IQueryable<OrganizationUser> Run(DatabaseContext dbContext)
    {
        var query = from ou in dbContext.OrganizationUsers
                    join u in dbContext.Users
                        on ou.UserId equals u.Id into u_g
                    from u in u_g.DefaultIfEmpty()
                    where ou.OrganizationId == _organizationId &&
                        ((!_onlyUsers && (ou.Email == _email || u.Email == _email))
                         || (_onlyUsers && u.Email == _email))
                    select ou;
        return query;
    }
}
