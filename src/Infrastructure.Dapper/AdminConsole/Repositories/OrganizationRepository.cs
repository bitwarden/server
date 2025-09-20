using System.Data;
using System.Data.Common;
using System.Text.Json;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums.Provider;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers.Models;
using Bit.Core.Auth.Entities;
using Bit.Core.Entities;
using Bit.Core.Models.Data.Organizations;
using Bit.Core.Models.Data.Organizations.OrganizationUsers;
using Bit.Core.Repositories;
using Bit.Core.Settings;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using OneOf;
using OneOf.Types;

#nullable enable

namespace Bit.Infrastructure.Dapper.Repositories;

public class OrganizationRepository : Repository<Organization, Guid>, IOrganizationRepository
{
    private readonly ILogger<OrganizationRepository> _logger;

    public OrganizationRepository(
        GlobalSettings globalSettings,
        ILogger<OrganizationRepository> logger)
        : base(globalSettings.SqlServer.ConnectionString, globalSettings.SqlServer.ReadOnlyConnectionString)
    {
        _logger = logger;
    }

    public async Task<Organization?> GetByIdentifierAsync(string identifier)
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
                new
                {
                    Name = name,
                    UserEmail = userEmail,
                    Paid = paid,
                    Skip = skip,
                    Take = take
                },
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

    public async Task<Organization?> GetByLicenseKeyAsync(string licenseKey)
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

    public async Task<SelfHostedOrganizationDetails?> GetSelfHostedOrganizationDetailsById(Guid id)
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

    public async Task<ICollection<Organization>> SearchUnassignedToProviderAsync(string name, string ownerEmail,
        int skip, int take)
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

    public async Task<IEnumerable<string>> GetOwnerEmailAddressesById(Guid organizationId)
    {
        _logger.LogInformation("AC-1758: Executing GetOwnerEmailAddressesById (Dapper)");

        await using var connection = new SqlConnection(ConnectionString);

        return await connection.QueryAsync<string>(
            $"[{Schema}].[{Table}_ReadOwnerEmailAddressesById]",
            new { OrganizationId = organizationId },
            commandType: CommandType.StoredProcedure);
    }

    public async Task<ICollection<Organization>> GetByVerifiedUserEmailDomainAsync(Guid userId)
    {
        using (var connection = new SqlConnection(ConnectionString))
        {
            var result = await connection.QueryAsync<Organization>(
                "[dbo].[Organization_ReadByClaimedUserEmailDomain]",
                new { UserId = userId },
                commandType: CommandType.StoredProcedure);

            return result.ToList();
        }
    }

    public async Task<ICollection<Organization>> GetAddableToProviderByUserIdAsync(
        Guid userId,
        ProviderType providerType)
    {
        using (var connection = new SqlConnection(ConnectionString))
        {
            var result = await connection.QueryAsync<Organization>(
                $"[{Schema}].[{Table}_ReadAddableToProviderByUserId]",
                new { UserId = userId, ProviderType = providerType },
                commandType: CommandType.StoredProcedure);

            return result.ToList();
        }
    }

    public async Task<ICollection<Organization>> GetManyByIdsAsync(IEnumerable<Guid> ids)
    {
        await using var connection = new SqlConnection(ConnectionString);
        return (await connection.QueryAsync<Organization>(
                $"[{Schema}].[{Table}_ReadManyByIds]",
                new { OrganizationIds = ids.ToGuidIdArrayTVP() },
                commandType: CommandType.StoredProcedure))
            .ToList();
    }

    public async Task<OrganizationSeatCounts> GetOccupiedSeatCountByOrganizationIdAsync(Guid organizationId)
    {
        await using var connection = new SqlConnection(ConnectionString);

        return await GetOccupiedSeatCountByOrganizationIdAsync(organizationId, connection);
    }

    public async Task<OrganizationSeatCounts> GetOccupiedSeatCountByOrganizationIdInTransactionAsync(Guid organizationId,
        DbTransaction transaction)
    {
        var connection = transaction.Connection;

        if (connection is null) throw new InvalidOperationException("Transaction connection is null");

        return await GetOccupiedSeatCountByOrganizationIdAsync(organizationId, connection);
    }

    private static async Task<OrganizationSeatCounts> GetOccupiedSeatCountByOrganizationIdAsync(
        Guid organizationId,
        DbConnection connection)
    {
        var result = await connection.QueryAsync<OrganizationSeatCounts>(
            "[dbo].[Organization_ReadOccupiedSeatCountByOrganizationId]",
            new { OrganizationId = organizationId },
            commandType: CommandType.StoredProcedure);

        return result.SingleOrDefault() ?? new OrganizationSeatCounts();
    }

    public async Task<IEnumerable<Organization>> GetOrganizationsForSubscriptionSyncAsync()
    {
        await using var connection = new SqlConnection(ConnectionString);

        return await connection.QueryAsync<Organization>(
            "[dbo].[Organization_GetOrganizationsForSubscriptionSync]",
            commandType: CommandType.StoredProcedure);
    }

    public async Task UpdateSuccessfulOrganizationSyncStatusAsync(IEnumerable<Guid> successfulOrganizations,
        DateTime syncDate)
    {
        await using var connection = new SqlConnection(ConnectionString);

        await connection.ExecuteAsync("[dbo].[Organization_UpdateSubscriptionStatus]",
            new { SuccessfulOrganizations = successfulOrganizations.ToGuidIdArrayTVP(), SyncDate = syncDate },
            commandType: CommandType.StoredProcedure);
    }

    public async Task IncrementSeatCountAsync(Guid organizationId, int increaseAmount, DateTime requestDate)
    {
        await using var connection = new SqlConnection(ConnectionString);

        await connection.ExecuteAsync("[dbo].[Organization_IncrementSeatCount]",
            new { OrganizationId = organizationId, SeatsToAdd = increaseAmount, RequestDate = requestDate },
            commandType: CommandType.StoredProcedure);
    }

    public async Task AddUsersToPasswordManagerAsync(
        Guid organizationId,
        DateTime requestDate,
        int passwordManagerSeatsToAdd,
        IEnumerable<CreateOrganizationUser> organizationUserCollection,
        DbTransaction transaction)
    {
        var organizationUsersList = organizationUserCollection.ToList();

        var connection = transaction.Connection;

        if (connection is null) throw new InvalidOperationException("Transaction connection is null");

        await connection.ExecuteAsync(
            $"[{Schema}].[OrganizationUser_CreateManyWithCollectionsAndGroups]",
            new
            {
                OrganizationUserData =
                    JsonSerializer.Serialize(organizationUsersList.Select(x => x.OrganizationUser)),
                CollectionData = JsonSerializer.Serialize(organizationUsersList
                    .SelectMany(x => x.Collections,
                        (user, collection) => new CollectionUser
                        {
                            CollectionId = collection.Id,
                            OrganizationUserId = user.OrganizationUser.Id,
                            ReadOnly = collection.ReadOnly,
                            HidePasswords = collection.HidePasswords,
                            Manage = collection.Manage
                        })),
                GroupData = JsonSerializer.Serialize(organizationUsersList
                    .SelectMany(x => x.Groups,
                        (user, group) => new GroupUser
                        {
                            GroupId = group,
                            OrganizationUserId = user.OrganizationUser.Id
                        }))
            },
            commandType: CommandType.StoredProcedure);

        if (passwordManagerSeatsToAdd > 0)
        {
            await connection.ExecuteAsync("[dbo].[Organization_IncrementSeatCount]",
                new
                {
                    OrganizationId = organizationId,
                    SeatsToAdd = passwordManagerSeatsToAdd,
                    RequestDate = requestDate
                },
                commandType: CommandType.StoredProcedure);
        }
    }

    public async Task<OneOf<Organization, None>> GetByIdInTransactionAsync(Guid organizationId,
        DbTransaction transaction)
    {
        var connection = transaction.Connection;

        if (connection is null) throw new InvalidOperationException("Transaction connection is null");

        var organizationResult = await connection.QueryAsync<Organization>(
            "[dbo].[Organization_ReadById]",
            new { Id = organizationId },
            commandType: CommandType.StoredProcedure);

        var organization = organizationResult.SingleOrDefault();

        return organization is null ? new None() : organization!;
    }
}
