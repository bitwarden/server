using AutoMapper;
using Bit.Core.AdminConsole.Repositories;
using Bit.Infrastructure.EntityFramework.AdminConsole.Models;
using Bit.Infrastructure.EntityFramework.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using AdminConsoleEntities = Bit.Core.AdminConsole.Entities;

namespace Bit.Infrastructure.EntityFramework.AdminConsole.Repositories;

public class OrganizationInviteLinkRepository
    : Repository<AdminConsoleEntities.OrganizationInviteLink, OrganizationInviteLink, Guid>,
      IOrganizationInviteLinkRepository
{
    public OrganizationInviteLinkRepository(
        IServiceScopeFactory serviceScopeFactory, IMapper mapper)
        : base(serviceScopeFactory, mapper,
               (DatabaseContext context) => context.OrganizationInviteLinks)
    { }

    public async Task<AdminConsoleEntities.OrganizationInviteLink?> GetByCodeAsync(Guid code)
    {
        using var scope = ServiceScopeFactory.CreateScope();
        var dbContext = GetDatabaseContext(scope);
        var result = await dbContext.OrganizationInviteLinks
            .FirstOrDefaultAsync(e => e.Code == code);
        return Mapper.Map<AdminConsoleEntities.OrganizationInviteLink>(result);
    }

    public async Task<AdminConsoleEntities.OrganizationInviteLink?> GetByOrganizationIdAsync(
        Guid organizationId)
    {
        using var scope = ServiceScopeFactory.CreateScope();
        var dbContext = GetDatabaseContext(scope);
        var result = await dbContext.OrganizationInviteLinks
            .FirstOrDefaultAsync(e => e.OrganizationId == organizationId);
        return Mapper.Map<AdminConsoleEntities.OrganizationInviteLink>(result);
    }
}
