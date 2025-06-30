using AutoMapper;
using AutoMapper.QueryableExtensions;
using Bit.Core.Auth.Enums;
using Bit.Core.Auth.Models.Data;
using Bit.Core.Repositories;
using Bit.Core.Settings;
using Bit.Infrastructure.EntityFramework.Auth.Models;
using Bit.Infrastructure.EntityFramework.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

#nullable enable

namespace Bit.Infrastructure.EntityFramework.Auth.Repositories;

public class AuthRequestRepository : Repository<Core.Auth.Entities.AuthRequest, AuthRequest, Guid>, IAuthRequestRepository
{
    private readonly IGlobalSettings _globalSettings;
    public AuthRequestRepository(IServiceScopeFactory serviceScopeFactory, IMapper mapper, IGlobalSettings globalSettings)
        : base(serviceScopeFactory, mapper, context => context.AuthRequests)
    {
        _globalSettings = globalSettings;
    }

    public async Task<int> DeleteExpiredAsync(
        TimeSpan userRequestExpiration, TimeSpan adminRequestExpiration, TimeSpan afterAdminApprovalExpiration)
    {

        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            var expiredRequests = await dbContext.AuthRequests
                .Where(a => (a.Type != AuthRequestType.AdminApproval && a.CreationDate.AddSeconds(userRequestExpiration.TotalSeconds) < DateTime.UtcNow)
                    || (a.Type == AuthRequestType.AdminApproval && a.Approved != true && a.CreationDate.AddSeconds(adminRequestExpiration.TotalSeconds) < DateTime.UtcNow)
                    || (a.Type == AuthRequestType.AdminApproval && a.Approved == true && a.ResponseDate!.Value.AddSeconds(afterAdminApprovalExpiration.TotalSeconds) < DateTime.UtcNow))
                .ToListAsync();
            dbContext.AuthRequests.RemoveRange(expiredRequests);
            return await dbContext.SaveChangesAsync();
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
                                             where ar.OrganizationId.Equals(organizationId) && ar.ResponseDate == null && ar.Type == AuthRequestType.AdminApproval
                                             select ar).ProjectTo<OrganizationAdminAuthRequest>(Mapper.ConfigurationProvider).ToListAsync();

            return orgUserAuthRequests;
        }
    }

    public async Task<IEnumerable<PendingAuthRequestDetails>> GetManyPendingAuthRequestByUserId(Guid userId)
    {
        var expirationMinutes = (int)_globalSettings.PasswordlessAuth.UserRequestExpiration.TotalMinutes;
        using var scope = ServiceScopeFactory.CreateScope();
        var dbContext = GetDatabaseContext(scope);
        var mostRecentAuthRequests = await
            (from authRequest in dbContext.AuthRequests
             where authRequest.Type == AuthRequestType.AuthenticateAndUnlock
                || authRequest.Type == AuthRequestType.Unlock
             where authRequest.UserId == userId
             where authRequest.CreationDate.AddMinutes(expirationMinutes) >= DateTime.UtcNow
             group authRequest by authRequest.RequestDeviceIdentifier into groupedAuthRequests
             select
                 (from r in groupedAuthRequests
                  join d in dbContext.Devices on new { r.RequestDeviceIdentifier, r.UserId }
                                            equals new { RequestDeviceIdentifier = d.Identifier, d.UserId } into deviceJoin
                  from dj in deviceJoin.DefaultIfEmpty() // This creates a left join allowing null for devices
                  orderby r.CreationDate descending
                  select new PendingAuthRequestDetails(r, dj.Id)).First()
             ).ToListAsync();

        mostRecentAuthRequests.RemoveAll(a => a.Approved != null);

        return mostRecentAuthRequests;
    }

    public async Task<ICollection<OrganizationAdminAuthRequest>> GetManyAdminApprovalRequestsByManyIdsAsync(
        Guid organizationId,
        IEnumerable<Guid> ids)
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            var orgUserAuthRequests = await (from ar in dbContext.AuthRequests
                                             where ar.OrganizationId.Equals(organizationId) && ids.Contains(ar.Id) && ar.Type == AuthRequestType.AdminApproval
                                             select ar).ProjectTo<OrganizationAdminAuthRequest>(Mapper.ConfigurationProvider).ToListAsync();

            return orgUserAuthRequests;
        }
    }

    public async Task UpdateManyAsync(IEnumerable<Core.Auth.Entities.AuthRequest> authRequests)
    {
        if (!authRequests.Any())
        {
            return;
        }

        var entities = new List<AuthRequest>();
        foreach (var authRequest in authRequests)
        {
            if (!authRequest.Id.Equals(default))
            {
                var entity = Mapper.Map<AuthRequest>(authRequest);
                entities.Add(entity);
            }
        }

        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            dbContext.UpdateRange(entities);
            await dbContext.SaveChangesAsync();
        }
    }
}
