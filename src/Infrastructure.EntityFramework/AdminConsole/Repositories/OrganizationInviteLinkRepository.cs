using AutoMapper;
using Bit.Core;
using Bit.Core.AdminConsole.Repositories;
using Bit.Infrastructure.EntityFramework.AdminConsole.Models;
using Bit.Infrastructure.EntityFramework.Repositories;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using AdminConsoleEntities = Bit.Core.AdminConsole.Entities;

namespace Bit.Infrastructure.EntityFramework.AdminConsole.Repositories;

public class OrganizationInviteLinkRepository
    : Repository<AdminConsoleEntities.OrganizationInviteLink, OrganizationInviteLink, Guid>,
      IOrganizationInviteLinkRepository
{
    private readonly IDataProtector _dataProtector;

    public OrganizationInviteLinkRepository(
        IServiceScopeFactory serviceScopeFactory, IMapper mapper,
        IDataProtectionProvider dataProtectionProvider)
        : base(serviceScopeFactory, mapper,
               (DatabaseContext context) => context.OrganizationInviteLinks)
    {
        _dataProtector = dataProtectionProvider.CreateProtector(Constants.DatabaseFieldProtectorPurpose);
    }

    public override async Task<AdminConsoleEntities.OrganizationInviteLink?> GetByIdAsync(Guid id)
    {
        var link = await base.GetByIdAsync(id);
        UnprotectData(link);
        return link;
    }

    public async Task<AdminConsoleEntities.OrganizationInviteLink?> GetByOrganizationIdAsync(
        Guid organizationId)
    {
        using var scope = ServiceScopeFactory.CreateScope();
        var dbContext = GetDatabaseContext(scope);
        var result = await dbContext.OrganizationInviteLinks
            .FirstOrDefaultAsync(e => e.OrganizationId == organizationId);
        var link = Mapper.Map<AdminConsoleEntities.OrganizationInviteLink>(result);
        UnprotectData(link);
        return link;
    }

    public override async Task<AdminConsoleEntities.OrganizationInviteLink> CreateAsync(
        AdminConsoleEntities.OrganizationInviteLink link)
    {
        await ProtectDataAndSaveAsync(link, () => base.CreateAsync(link));
        return link;
    }

    public override async Task ReplaceAsync(AdminConsoleEntities.OrganizationInviteLink link)
    {
        await ProtectDataAndSaveAsync(link, () => base.ReplaceAsync(link));
    }

    public async Task RefreshAsync(
        AdminConsoleEntities.OrganizationInviteLink oldLink,
        AdminConsoleEntities.OrganizationInviteLink newLink)
    {
        var originalCode = newLink.Code;
        ProtectData(newLink);
        try
        {
            using var scope = ServiceScopeFactory.CreateScope();
            var dbContext = GetDatabaseContext(scope);
            await using var transaction = await dbContext.Database.BeginTransactionAsync();

            await dbContext.OrganizationInviteLinks
                .Where(e => e.Id == oldLink.Id)
                .ExecuteDeleteAsync();

            var efNew = Mapper.Map<OrganizationInviteLink>(newLink);
            await dbContext.OrganizationInviteLinks.AddAsync(efNew);
            await dbContext.SaveChangesAsync();
            await transaction.CommitAsync();
        }
        finally
        {
            newLink.Code = originalCode;
        }
    }

    private async Task ProtectDataAndSaveAsync(
        AdminConsoleEntities.OrganizationInviteLink link, Func<Task> saveTask)
    {
        var originalCode = link.Code;
        ProtectData(link);
        try
        {
            await saveTask();
        }
        finally
        {
            link.Code = originalCode;
        }
    }

    private void ProtectData(AdminConsoleEntities.OrganizationInviteLink link)
    {
        if (!link.Code?.StartsWith(Constants.DatabaseFieldProtectedPrefix) ?? false)
        {
            link.Code = string.Concat(Constants.DatabaseFieldProtectedPrefix,
                _dataProtector.Protect(link.Code!));
        }
    }

    private void UnprotectData(AdminConsoleEntities.OrganizationInviteLink? link)
    {
        if (link?.Code?.StartsWith(Constants.DatabaseFieldProtectedPrefix) ?? false)
        {
            link.Code = _dataProtector.Unprotect(
                link.Code.Substring(Constants.DatabaseFieldProtectedPrefix.Length));
        }
    }
}
