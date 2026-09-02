using System.Data;
using Bit.Core.Dirt.Reports.Models.Data;
using Bit.Core.Dirt.Reports.Repositories;
using Bit.Core.Settings;
using Bit.Infrastructure.Dapper.Repositories;
using Dapper;
using Microsoft.Data.SqlClient;

namespace Bit.Infrastructure.Dapper.Dirt;

public class OrganizationMemberBaseDetailRepository : BaseRepository, IOrganizationMemberBaseDetailRepository
{
    public OrganizationMemberBaseDetailRepository(GlobalSettings globalSettings)
        : this(globalSettings.SqlServer.ConnectionString, globalSettings.SqlServer.ReadOnlyConnectionString)
    {
    }

    public OrganizationMemberBaseDetailRepository(string connectionString, string readOnlyConnectionString) : base(
        connectionString, readOnlyConnectionString)
    {
    }

    public async Task<IEnumerable<OrganizationMemberBaseDetail>> GetOrganizationMemberBaseDetailsByOrganizationId(
        Guid organizationId)
    {
        await using var connection = new SqlConnection(ConnectionString);


        var result = await connection.QueryAsync<OrganizationMemberBaseDetail>(
            "[dbo].[MemberAccessReport_GetMemberAccessCipherDetailsByOrganizationId]",
            new
            {
                OrganizationId = organizationId

            }, commandType: CommandType.StoredProcedure);

        return result;
    }
}
