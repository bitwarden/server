// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using AutoMapper;
using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.Models.Data.Organizations.Policies;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Enums;
using Bit.Infrastructure.EntityFramework.AdminConsole.Models;
using Bit.Infrastructure.EntityFramework.AdminConsole.Repositories.Queries;
using Bit.Infrastructure.EntityFramework.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using AdminConsoleEntities = Bit.Core.AdminConsole.Entities;

namespace Bit.Infrastructure.EntityFramework.AdminConsole.Repositories;

public class PolicyRepository : Repository<AdminConsoleEntities.Policy, Policy, Guid>, IPolicyRepository
{
    public PolicyRepository(IServiceScopeFactory serviceScopeFactory, IMapper mapper)
        : base(serviceScopeFactory, mapper, (DatabaseContext context) => context.Policies)
    { }

    public async Task<AdminConsoleEntities.Policy> GetByOrganizationIdTypeAsync(Guid organizationId, PolicyType type)
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            var results = await dbContext.Policies
                .FirstOrDefaultAsync(p => p.OrganizationId == organizationId && p.Type == type);
            return Mapper.Map<AdminConsoleEntities.Policy>(results);
        }
    }

    public async Task<ICollection<AdminConsoleEntities.Policy>> GetManyByOrganizationIdAsync(Guid organizationId)
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            var results = await dbContext.Policies
                .Where(p => p.OrganizationId == organizationId)
                .ToListAsync();
            return Mapper.Map<List<AdminConsoleEntities.Policy>>(results);
        }
    }

    public async Task<ICollection<AdminConsoleEntities.Policy>> GetManyByUserIdAsync(Guid userId)
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);

            var query = new PolicyReadByUserIdQuery(userId);
            var results = await query.Run(dbContext).ToListAsync();
            return Mapper.Map<List<AdminConsoleEntities.Policy>>(results);
        }
    }

    public async Task<IEnumerable<PolicyDetails>> GetPolicyDetailsByUserId(Guid userId)
    {
        using var scope = ServiceScopeFactory.CreateScope();
        var dbContext = GetDatabaseContext(scope);

        var providerOrganizations = from pu in dbContext.ProviderUsers
                                    where pu.UserId == userId
                                    join po in dbContext.ProviderOrganizations
                                        on pu.ProviderId equals po.ProviderId
                                    select po;

        var query = from p in dbContext.Policies
                    join ou in dbContext.OrganizationUsers
                        on p.OrganizationId equals ou.OrganizationId
                    join o in dbContext.Organizations
                        on p.OrganizationId equals o.Id
                    where
                        p.Enabled &&
                        o.Enabled &&
                        o.UsePolicies &&
                        (
                            (ou.Status != OrganizationUserStatusType.Invited && ou.UserId == userId) ||
                            // Invited orgUsers do not have a UserId associated with them, so we have to match up their email
                            (ou.Status == OrganizationUserStatusType.Invited && ou.Email == dbContext.Users.Find(userId).Email)
                        )
                    select new PolicyDetails
                    {
                        OrganizationUserId = ou.Id,
                        OrganizationId = p.OrganizationId,
                        PolicyType = p.Type,
                        PolicyData = p.Data,
                        OrganizationUserType = ou.Type,
                        OrganizationUserStatus = ou.Status,
                        OrganizationUserPermissionsData = ou.Permissions,
                        IsProvider = providerOrganizations.Any(po => po.OrganizationId == p.OrganizationId)
                    };
        return await query.ToListAsync();
    }

    public async Task<IEnumerable<PolicyDetails>> PolicyDetailsReadByOrganizationIdAsync(Guid organizationId, PolicyType policyType)
    {
        using var scope = ServiceScopeFactory.CreateScope();
        var dbContext = GetDatabaseContext(scope);

        var givenOrgUsers =
            from ou in dbContext.OrganizationUsers
            where ou.OrganizationId == organizationId
            from u in dbContext.Users
            where
                u.Email == ou.Email
                || ou.UserId == u.Id
            select new
            {
                ou.Id,
                ou.OrganizationId,
                UserId = u.Id,
                u.Email
            };

        var orgUsersLinkedByEmail = from row in dbContext.OrganizationUsers
                .Join(
                    givenOrgUsers,
                    ou => ou.UserId,
                    gou => gou.UserId,
                    (ou, gou) => new { ou, gou }
                )
                                    select new
                                    {
                                        row.ou.Id,
                                        row.ou.OrganizationId,
                                        row.gou.UserId,
                                        row.ou.Type,
                                        row.ou.Status,
                                        row.ou.Permissions
                                    };

        var orgUsersLinkedByUserId = from row in dbContext.OrganizationUsers
                .Join(
                    givenOrgUsers,
                    ou => ou.Email,
                    gou => gou.Email,
                    (ou, gou) => new { ou, gou }
                )
                                     select new
                                     {
                                         row.ou.Id,
                                         row.ou.OrganizationId,
                                         row.gou.UserId,
                                         row.ou.Type,
                                         row.ou.Status,
                                         row.ou.Permissions
                                     };

        var allAffectedOrgUsers = orgUsersLinkedByEmail.Union(orgUsersLinkedByUserId);

        var providerOrganizations = from pu in dbContext.ProviderUsers
                                    join po in dbContext.ProviderOrganizations
                                        on pu.ProviderId equals po.ProviderId
                                    join ou in allAffectedOrgUsers
                                        on pu.UserId equals ou.UserId
                                    where pu.UserId == ou.UserId
                                    select new
                                    {
                                        pu.UserId,
                                        po.OrganizationId
                                    };

        var policyWithAffectedUsers =
            from p in dbContext.Policies
            join o in dbContext.Organizations
                on p.OrganizationId equals o.Id
            join ou in allAffectedOrgUsers
                on o.Id equals ou.OrganizationId
            where p.Enabled
                   && o.Enabled
                   && o.UsePolicies
                   && p.Type == policyType
            select new PolicyDetails
            {
                OrganizationUserId = ou.Id,
                OrganizationId = p.OrganizationId,
                PolicyType = p.Type,
                PolicyData = p.Data,
                OrganizationUserType = ou.Type,
                OrganizationUserStatus = ou.Status,
                OrganizationUserPermissionsData = ou.Permissions,
                IsProvider = providerOrganizations.Any(po => po.OrganizationId == p.OrganizationId)
            };

        return await policyWithAffectedUsers.ToListAsync();
    }
}
