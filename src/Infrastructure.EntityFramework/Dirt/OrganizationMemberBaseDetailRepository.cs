using AutoMapper;
using Bit.Core.Dirt.Reports.Models.Data;
using Bit.Core.Dirt.Reports.Repositories;
using Bit.Infrastructure.EntityFramework.Repositories;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Bit.Infrastructure.EntityFramework.Dirt;

public class OrganizationMemberBaseDetailRepository : BaseEntityFrameworkRepository, IOrganizationMemberBaseDetailRepository
{
    public OrganizationMemberBaseDetailRepository(IServiceScopeFactory serviceScopeFactory, IMapper mapper) : base(
        serviceScopeFactory,
        mapper)
    {
    }

    public async Task<IEnumerable<OrganizationMemberBaseDetail>> GetOrganizationMemberBaseDetailsByOrganizationId(
        Guid organizationId)
    {
        await using var scope = ServiceScopeFactory.CreateAsyncScope();
        var dbContext = GetDatabaseContext(scope);

        var result = await dbContext.Set<OrganizationMemberBaseDetail>()
            .FromSqlRaw("EXEC [dbo].[MemberAccessReport_GetMemberAccessCipherDetailsByOrganizationId] @OrganizationId",
                new SqlParameter("@OrganizationId", organizationId))
            .ToListAsync();

        return result;
    }
}
