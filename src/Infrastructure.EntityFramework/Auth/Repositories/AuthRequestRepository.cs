using AutoMapper;
using Bit.Core.Auth.Enums;
using Bit.Core.Auth.Models.Data;
using Bit.Core.Repositories;
using Bit.Infrastructure.EntityFramework.Auth.Models;
using Bit.Infrastructure.EntityFramework.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Bit.Infrastructure.EntityFramework.Auth.Repositories;

public class AuthRequestRepository : Repository<Core.Auth.Entities.AuthRequest, AuthRequest, Guid>, IAuthRequestRepository
{
    public AuthRequestRepository(IServiceScopeFactory serviceScopeFactory, IMapper mapper)
        : base(serviceScopeFactory, mapper, (DatabaseContext context) => context.AuthRequests)
    { }
    public async Task<int> DeleteExpiredAsync()
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            var expiredRequests = await dbContext.AuthRequests.Where(a => a.CreationDate < DateTime.Now.AddMinutes(-15)).ToListAsync();
            dbContext.AuthRequests.RemoveRange(expiredRequests);
            await dbContext.SaveChangesAsync();
            return 1;
        }
    }

    public async Task<ICollection<Core.Auth.Entities.AuthRequest>> GetManyByUserIdAsync(Guid userId)
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            var userAuthRequests = await dbContext.AuthRequests.Where(a => a.UserId.Equals(userId)).ToListAsync();
            return Mapper.Map<List<Core.Auth.Entities.AuthRequest>>(userAuthRequests);
        }
    }

    public async Task<ICollection<OrganizationAdminAuthRequest>> GetManyPendingByOrganizationIdAsync(Guid organizationId)
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            var orgUserAuthRequests = await (from ar in dbContext.AuthRequests
                                             join ou in dbContext.OrganizationUsers on ar.UserId equals ou.UserId
                                             where ou.OrganizationId.Equals(organizationId) && ar.ResponseDate == null && ar.Type == AuthRequestType.AdminApproval
                                             select new { ar, ou }).ToListAsync();

            return orgUserAuthRequests.Select(t =>
            {
                var authRequestWithEmail = Mapper.Map<OrganizationAdminAuthRequest>(t.ar);
                authRequestWithEmail.Email = t.ou.Email;
                authRequestWithEmail.OrganizationId = t.ou.OrganizationId;
                authRequestWithEmail.OrganizationUserId = t.ou.Id;
                return authRequestWithEmail;
            }).ToList();
        }
    }
}
