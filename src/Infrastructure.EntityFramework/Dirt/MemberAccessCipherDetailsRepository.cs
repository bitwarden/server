using AutoMapper;
using Bit.Core.Dirt.Reports.Models.Data;
using Bit.Core.Dirt.Reports.Repositories;
using Bit.Infrastructure.EntityFramework.Repositories;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Bit.Infrastructure.EntityFramework.Dirt;

public class MemberAccessCipherDetailsRepository : BaseEntityFrameworkRepository, IMemberAccessCipherDetailsRepository
{
    public MemberAccessCipherDetailsRepository(IServiceScopeFactory serviceScopeFactory, IMapper mapper) : base(
        serviceScopeFactory,
        mapper)
    {
    }

    public async Task<IEnumerable<MemberAccessCipherDetails>> GetMemberAccessCipherDetailsByOrganizationId(Guid organizationId)
    {
        await using var scope = ServiceScopeFactory.CreateAsyncScope();
        var dbContext = GetDatabaseContext(scope);

        var result = await dbContext.Set<MemberAccessCipherDetails>()
            .FromSqlRaw("EXEC [dbo].[MemberAccessReport_GetMemberAccessCipherDetailsByOrganizationId] @OrganizationId",
                new SqlParameter("@OrganizationId", organizationId))
            .ToListAsync();

        return result;
    }
}
