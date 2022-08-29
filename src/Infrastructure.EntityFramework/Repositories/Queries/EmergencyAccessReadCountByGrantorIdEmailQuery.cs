using Bit.Infrastructure.EntityFramework.Models;

namespace Bit.Infrastructure.EntityFramework.Repositories.Queries;

public class EmergencyAccessReadCountByGrantorIdEmailQuery : IQuery<EmergencyAccess>
{
    private readonly Guid _grantorId;
    private readonly string _email;
    private readonly bool _onlyRegisteredUsers;

    public EmergencyAccessReadCountByGrantorIdEmailQuery(Guid grantorId, string email, bool onlyRegisteredUsers)
    {
        _grantorId = grantorId;
        _email = email;
        _onlyRegisteredUsers = onlyRegisteredUsers;
    }

    public IQueryable<EmergencyAccess> Run(DatabaseContext dbContext)
    {
        var query = from ea in dbContext.EmergencyAccesses
                    join u in dbContext.Users
                        on ea.GranteeId equals u.Id into u_g
                    from u in u_g.DefaultIfEmpty()
                    where ea.GrantorId == _grantorId &&
                        ((!_onlyRegisteredUsers && (ea.Email == _email || u.Email == _email))
                         || (_onlyRegisteredUsers && u.Email == _email))
                    select ea;
        return query;
    }
}
