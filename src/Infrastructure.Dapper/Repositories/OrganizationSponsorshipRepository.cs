using System.Data;
using System.Data.SqlClient;
using Bit.Core.Entities;
using Bit.Core.Repositories;
using Bit.Core.Settings;
using Dapper;

namespace Bit.Infrastructure.Dapper.Repositories;

public class OrganizationSponsorshipRepository : Repository<OrganizationSponsorship, Guid>, IOrganizationSponsorshipRepository
{
    public OrganizationSponsorshipRepository(GlobalSettings globalSettings)
        : this(globalSettings.SqlServer.ConnectionString, globalSettings.SqlServer.ReadOnlyConnectionString)
    { }

    public OrganizationSponsorshipRepository(string connectionString, string readOnlyConnectionString)
        : base(connectionString, readOnlyConnectionString)
    { }

    public async Task<ICollection<Guid>> CreateManyAsync(IEnumerable<OrganizationSponsorship> organizationSponsorships)
    {
        if (!organizationSponsorships.Any())
        {
            return default;
        }

        foreach (var organizationSponsorship in organizationSponsorships)
        {
            organizationSponsorship.SetNewId();
        }

        var orgSponsorshipsTVP = organizationSponsorships.ToTvp();
        using (var connection = new SqlConnection(ConnectionString))
        {
            var results = await connection.ExecuteAsync(
                $"[dbo].[OrganizationSponsorship_CreateMany]",
                new { OrganizationSponsorshipsInput = orgSponsorshipsTVP },
                commandType: CommandType.StoredProcedure);
        }

        return organizationSponsorships.Select(u => u.Id).ToList();
    }

    public async Task ReplaceManyAsync(IEnumerable<OrganizationSponsorship> organizationSponsorships)
    {
        if (!organizationSponsorships.Any())
        {
            return;
        }

        var orgSponsorshipsTVP = organizationSponsorships.ToTvp();
        using (var connection = new SqlConnection(ConnectionString))
        {
            var results = await connection.ExecuteAsync(
                $"[dbo].[OrganizationSponsorship_UpdateMany]",
                new { OrganizationSponsorshipsInput = orgSponsorshipsTVP },
                commandType: CommandType.StoredProcedure);
        }
    }

    public async Task UpsertManyAsync(IEnumerable<OrganizationSponsorship> organizationSponsorships)
    {
        var createSponsorships = new List<OrganizationSponsorship>();
        var replaceSponsorships = new List<OrganizationSponsorship>();
        foreach (var organizationSponsorship in organizationSponsorships)
        {
            if (organizationSponsorship.Id.Equals(default))
            {
                createSponsorships.Add(organizationSponsorship);
            }
            else
            {
                replaceSponsorships.Add(organizationSponsorship);
            }
        }

        await CreateManyAsync(createSponsorships);
        await ReplaceManyAsync(replaceSponsorships);
    }

    public async Task DeleteManyAsync(IEnumerable<Guid> organizationSponsorshipIds)
    {
        using (var connection = new SqlConnection(ConnectionString))
        {
            await connection.ExecuteAsync("[dbo].[OrganizationSponsorship_DeleteByIds]",
                new { Ids = organizationSponsorshipIds.ToGuidIdArrayTVP() }, commandType: CommandType.StoredProcedure);
        }
    }

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

    public async Task<DateTime?> GetLatestSyncDateBySponsoringOrganizationIdAsync(Guid sponsoringOrganizationId)
    {
        using (var connection = new SqlConnection(ConnectionString))
        {
            return await connection.QuerySingleOrDefaultAsync<DateTime?>(
                "[dbo].[OrganizationSponsorship_ReadLatestBySponsoringOrganizationId]",
                new { SponsoringOrganizationId = sponsoringOrganizationId },
                commandType: CommandType.StoredProcedure);
        }
    }

    public async Task<ICollection<OrganizationSponsorship>> GetManyBySponsoringOrganizationAsync(Guid sponsoringOrganizationId)
    {
        using (var connection = new SqlConnection(ConnectionString))
        {
            var results = await connection.QueryAsync<OrganizationSponsorship>(
                "[dbo].[OrganizationSponsorship_ReadBySponsoringOrganizationId]",
                new
                {
                    SponsoringOrganizationId = sponsoringOrganizationId
                },
                commandType: CommandType.StoredProcedure);

            return results.ToList();
        }
    }

}
