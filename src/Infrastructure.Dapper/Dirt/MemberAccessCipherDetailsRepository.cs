using System.Data;
using Bit.Core.Dirt.Reports.Models.Data;
using Bit.Core.Dirt.Reports.Repositories;
using Bit.Core.Settings;
using Bit.Infrastructure.Dapper.Repositories;
using Dapper;
using Microsoft.Data.SqlClient;

namespace Bit.Infrastructure.Dapper.Dirt;

public class MemberAccessCipherDetailsRepository : BaseRepository, IMemberAccessCipherDetailsRepository
{
    public MemberAccessCipherDetailsRepository(GlobalSettings globalSettings)
        : this(globalSettings.SqlServer.ConnectionString, globalSettings.SqlServer.ReadOnlyConnectionString)
    {
    }

    public MemberAccessCipherDetailsRepository(string connectionString, string readOnlyConnectionString) : base(
        connectionString, readOnlyConnectionString)
    {
    }

    public async Task<IEnumerable<MemberAccessCipherDetails>> GetMemberAccessCipherDetailsByOrganizationId(Guid organizationId)
    {
        await using var connection = new SqlConnection(ConnectionString);


        var result = await connection.QueryAsync<MemberAccessCipherDetails>(
            "[dbo].[MemberAccessReport_GetMemberAccessCipherDetailsByOrganizationId]",
            new
            {
                OrganizationId = organizationId

            }, commandType: CommandType.StoredProcedure);

        return result;

    }

}
