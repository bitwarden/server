using System;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;
using Bit.Core.Models.Table;
using Bit.Core.Settings;
using Dapper;

namespace Bit.Core.Repositories.SqlServer
{
    public class OrganizationSponsorshipRepository : Repository<OrganizationSponsorship, Guid>, IOrganizationSponsorshipRepository
    {
        public OrganizationSponsorshipRepository(GlobalSettings globalSettings)
            : this(globalSettings.SqlServer.ConnectionString, globalSettings.SqlServer.ReadOnlyConnectionString)
        { }

        public OrganizationSponsorshipRepository(string connectionString, string readOnlyConnectionString)
            : base(connectionString, readOnlyConnectionString)
        { }

        public async Task<OrganizationSponsorship> GetBySponsoringOrganizationUserIdAsync(Guid sponsoringOrganizationUserId)
        {
            using (var connection = new SqlConnection(ConnectionString))
            {
                var results = await connection.QueryAsync<OrganizationSponsorship>(
                    "[dbo].[OrganizationSponsorship_ReadBySponsoringOrganizationUserId]",
                    new
                    {
                        SponsoringOrganizationUserId = sponsoringOrganizationUserId
                    },
                    commandType: CommandType.StoredProcedure);

                return results.SingleOrDefault();
            }
        }

        public async Task<OrganizationSponsorship> GetBySponsoredOrganizationIdAsync(Guid sponsoredOrganizationId)
        {
            using (var connection = new SqlConnection(ConnectionString))
            {
                var results = await connection.QueryAsync<OrganizationSponsorship>(
                    "[dbo].[OrganizationSponsorship_ReadBySponsoredOrganizationId]",
                    new { SponsoredOrganizationId = sponsoredOrganizationId },
                    commandType: CommandType.StoredProcedure);

                return results.SingleOrDefault();
            }
        }

        public async Task<OrganizationSponsorship> GetByOfferedToEmailAsync(string offeredToEmail)
        {
            using (var connection = new SqlConnection(ConnectionString))
            {
                var results = await connection.QueryAsync<OrganizationSponsorship>(
                    "[dbo].[OrganizationSponsorship_ReadByOfferedToEmail]",
                    new
                    {
                        OfferedToEmail = offeredToEmail
                    },
                    commandType: CommandType.StoredProcedure);

                return results.SingleOrDefault();
            }
        }
    }
}
