using System.Data;
using Bit.Core.Entities;
using Bit.Core.Models.Data.Organizations;
using Bit.Core.Repositories;
using Bit.Core.Settings;
using Dapper;
using Microsoft.Data.SqlClient;

namespace Bit.Infrastructure.Dapper.Repositories;

public class OrganizationRepository : Repository<Organization, Guid>, IOrganizationRepository
{
    public OrganizationRepository(GlobalSettings globalSettings)
        : this(globalSettings.SqlServer.ConnectionString, globalSettings.SqlServer.ReadOnlyConnectionString)
    { }

    public OrganizationRepository(string connectionString, string readOnlyConnectionString)
        : base(connectionString, readOnlyConnectionString)
    { }

    public async Task<Organization> GetByIdentifierAsync(string identifier)
    {
        using (var connection = new SqlConnection(ConnectionString))
        {
            var results = await connection.QueryAsync<Organization>(
                "[dbo].[Organization_ReadByIdentifier]",
                new { Identifier = identifier },
                commandType: CommandType.StoredProcedure);

            return results.SingleOrDefault();
        }
    }

    public async Task<ICollection<Organization>> GetManyByEnabledAsync()
    {
        using (var connection = new SqlConnection(ConnectionString))
        {
            var results = await connection.QueryAsync<Organization>(
                "[dbo].[Organization_ReadByEnabled]",
                commandType: CommandType.StoredProcedure);

            return results.ToList();
        }
    }

    public async Task<ICollection<Organization>> GetManyByUserIdAsync(Guid userId)
    {
        using (var connection = new SqlConnection(ConnectionString))
        {
            var results = await connection.QueryAsync<Organization>(
                "[dbo].[Organization_ReadByUserId]",
                new { UserId = userId },
                commandType: CommandType.StoredProcedure);

            return results.ToList();
        }
    }

    public async Task<ICollection<Organization>> SearchAsync(string name, string userEmail, bool? paid,
        int skip, int take)
    {
        using (var connection = new SqlConnection(ReadOnlyConnectionString))
        {
            var results = await connection.QueryAsync<Organization>(
                "[dbo].[Organization_Search]",
                new { Name = name, UserEmail = userEmail, Paid = paid, Skip = skip, Take = take },
                commandType: CommandType.StoredProcedure,
                commandTimeout: 120);

            return results.ToList();
        }
    }

    public async Task UpdateStorageAsync(Guid id)
    {
        using (var connection = new SqlConnection(ConnectionString))
        {
            await connection.ExecuteAsync(
                "[dbo].[Organization_UpdateStorage]",
                new { Id = id },
                commandType: CommandType.StoredProcedure,
                commandTimeout: 180);
        }
    }

    public async Task<ICollection<OrganizationAbility>> GetManyAbilitiesAsync()
    {
        using (var connection = new SqlConnection(ConnectionString))
        {
            var results = await connection.QueryAsync<OrganizationAbility>(
                "[dbo].[Organization_ReadAbilities]",
                commandType: CommandType.StoredProcedure);

            return results.ToList();
        }
    }

    public async Task<Organization> GetByLicenseKeyAsync(string licenseKey)
    {
        using (var connection = new SqlConnection(ConnectionString))
        {
            var result = await connection.QueryAsync<Organization>(
                "[dbo].[Organization_ReadByLicenseKey]",
                new { LicenseKey = licenseKey },
                commandType: CommandType.StoredProcedure);

            return result.SingleOrDefault();
        }
    }

    public async Task<SelfHostedOrganizationDetails> GetSelfHostedOrganizationDetailsById(Guid id)
    {
        using (var connection = new SqlConnection(ConnectionString))
        {
            var result = await connection.QueryMultipleAsync(
                "[dbo].[Organization_ReadSelfHostedDetailsById]",
                new { Id = id },
                commandType: CommandType.StoredProcedure);

            var selfHostOrganization = await result.ReadSingleOrDefaultAsync<SelfHostedOrganizationDetails>();
            if (selfHostOrganization == null)
            {
                return null;
            }

            selfHostOrganization.OccupiedSeatCount = await result.ReadSingleAsync<int>();
            selfHostOrganization.CollectionCount = await result.ReadSingleAsync<int>();
            selfHostOrganization.GroupCount = await result.ReadSingleAsync<int>();
            selfHostOrganization.OrganizationUsers = await result.ReadAsync<OrganizationUser>();
            selfHostOrganization.Policies = await result.ReadAsync<Policy>();
            selfHostOrganization.SsoConfig = await result.ReadFirstOrDefaultAsync<SsoConfig>();
            selfHostOrganization.ScimConnections = await result.ReadAsync<OrganizationConnection>();

            return selfHostOrganization;
        }
    }

	public async Task<ICollection<Organization>> SearchUnassignedToProviderAsync(string name, string ownerEmail, int skip, int take)
    {
        using (var connection = new SqlConnection(ReadOnlyConnectionString))
        {
            var results = await connection.QueryAsync<Organization>(
                "[dbo].[Organization_UnassignedToProviderSearch]",
                new { Name = name, OwnerEmail = ownerEmail, Skip = skip, Take = take },
                commandType: CommandType.StoredProcedure,
                commandTimeout: 120);

            return results.ToList();
		}
	}
}
