using System.Data;
using System.Text.Json;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.KeyManagement.UserKey;
using Bit.Core.Models.Data;
using Bit.Core.Models.Data.Organizations.OrganizationUsers;
using Bit.Core.Repositories;
using Bit.Core.Settings;
using Dapper;
using Microsoft.Data.SqlClient;

#nullable enable

namespace Bit.Infrastructure.Dapper.Repositories;

public class OrganizationUserRepository : Repository<OrganizationUser, Guid>, IOrganizationUserRepository
{
    /// <summary>
    /// For use with methods with TDS stream issues.
    /// This has been observed in Linux-hosted SqlServers with large table-valued-parameters
    /// https://github.com/dotnet/SqlClient/issues/54
    /// </summary>
    private string _marsConnectionString;

    public OrganizationUserRepository(GlobalSettings globalSettings)
        : base(globalSettings.SqlServer.ConnectionString, globalSettings.SqlServer.ReadOnlyConnectionString)
    {
        var builder = new SqlConnectionStringBuilder(ConnectionString)
        {
            MultipleActiveResultSets = true,
        };
        _marsConnectionString = builder.ToString();
    }

    public async Task<int> GetCountByOrganizationIdAsync(Guid organizationId)
    {
        using (var connection = new SqlConnection(ConnectionString))
        {
            var results = await connection.ExecuteScalarAsync<int>(
                "[dbo].[OrganizationUser_ReadCountByOrganizationId]",
                new { OrganizationId = organizationId },
                commandType: CommandType.StoredProcedure);

            return results;
        }
    }

    public async Task<int> GetCountByFreeOrganizationAdminUserAsync(Guid userId)
    {
        using (var connection = new SqlConnection(ConnectionString))
        {
            var results = await connection.ExecuteScalarAsync<int>(
                "[dbo].[OrganizationUser_ReadCountByFreeOrganizationAdminUser]",
                new { UserId = userId },
                commandType: CommandType.StoredProcedure);

            return results;
        }
    }

    public async Task<int> GetCountByOnlyOwnerAsync(Guid userId)
    {
        using (var connection = new SqlConnection(ConnectionString))
        {
            var results = await connection.ExecuteScalarAsync<int>(
                "[dbo].[OrganizationUser_ReadCountByOnlyOwner]",
                new { UserId = userId },
                commandType: CommandType.StoredProcedure);

            return results;
        }
    }

    public async Task<int> GetCountByOrganizationAsync(Guid organizationId, string email, bool onlyRegisteredUsers)
    {
        using (var connection = new SqlConnection(ConnectionString))
        {
            var result = await connection.ExecuteScalarAsync<int>(
                "[dbo].[OrganizationUser_ReadCountByOrganizationIdEmail]",
                new { OrganizationId = organizationId, Email = email, OnlyUsers = onlyRegisteredUsers },
                commandType: CommandType.StoredProcedure);

            return result;
        }
    }

    public async Task<int> GetOccupiedSeatCountByOrganizationIdAsync(Guid organizationId)
    {
        using (var connection = new SqlConnection(ConnectionString))
        {
            var result = await connection.ExecuteScalarAsync<int>(
                "[dbo].[OrganizationUser_ReadOccupiedSeatCountByOrganizationId]",
                new { OrganizationId = organizationId },
                commandType: CommandType.StoredProcedure);

            return result;
        }
    }

    public async Task<int> GetOccupiedSmSeatCountByOrganizationIdAsync(Guid organizationId)
    {
        using (var connection = new SqlConnection(ConnectionString))
        {
            var result = await connection.ExecuteScalarAsync<int>(
                "[dbo].[OrganizationUser_ReadOccupiedSmSeatCountByOrganizationId]",
                new { OrganizationId = organizationId },
                commandType: CommandType.StoredProcedure);

            return result;
        }
    }

    public async Task<ICollection<string>> SelectKnownEmailsAsync(Guid organizationId, IEnumerable<string> emails,
        bool onlyRegisteredUsers)
    {
        var emailsTvp = emails.ToArrayTVP("Email");
        using (var connection = new SqlConnection(_marsConnectionString))
        {
            var result = await connection.QueryAsync<string>(
                "[dbo].[OrganizationUser_SelectKnownEmails]",
                new { OrganizationId = organizationId, Emails = emailsTvp, OnlyUsers = onlyRegisteredUsers },
                commandType: CommandType.StoredProcedure);

            // Return as a list to avoid timing out the sql connection
            return result.ToList();
        }
    }

    public async Task<OrganizationUser?> GetByOrganizationAsync(Guid organizationId, Guid userId)
    {
        using (var connection = new SqlConnection(ConnectionString))
        {
            var results = await connection.QueryAsync<OrganizationUser>(
                "[dbo].[OrganizationUser_ReadByOrganizationIdUserId]",
                new { OrganizationId = organizationId, UserId = userId },
                commandType: CommandType.StoredProcedure);

            return results.SingleOrDefault();
        }
    }

    public async Task<ICollection<OrganizationUser>> GetManyByUserAsync(Guid userId)
    {
        using (var connection = new SqlConnection(ConnectionString))
        {
            var results = await connection.QueryAsync<OrganizationUser>(
                "[dbo].[OrganizationUser_ReadByUserId]",
                new { UserId = userId },
                commandType: CommandType.StoredProcedure);

            return results.ToList();
        }
    }

    public async Task<ICollection<OrganizationUser>> GetManyByOrganizationAsync(Guid organizationId,
        OrganizationUserType? type)
    {
        using (var connection = new SqlConnection(ConnectionString))
        {
            var results = await connection.QueryAsync<OrganizationUser>(
                "[dbo].[OrganizationUser_ReadByOrganizationId]",
                new { OrganizationId = organizationId, Type = type },
                commandType: CommandType.StoredProcedure);

            return results.ToList();
        }
    }

    public async Task<Tuple<OrganizationUser?, ICollection<CollectionAccessSelection>>> GetByIdWithCollectionsAsync(Guid id)
    {
        using (var connection = new SqlConnection(ConnectionString))
        {
            var results = await connection.QueryMultipleAsync(
                "[dbo].[OrganizationUser_ReadWithCollectionsById]",
                new { Id = id },
                commandType: CommandType.StoredProcedure);

            var user = (await results.ReadAsync<OrganizationUser>()).SingleOrDefault();
            var collections = (await results.ReadAsync<CollectionAccessSelection>()).ToList();
            return new Tuple<OrganizationUser?, ICollection<CollectionAccessSelection>>(user, collections);
        }
    }

    public async Task<OrganizationUserUserDetails?> GetDetailsByIdAsync(Guid id)
    {
        using (var connection = new SqlConnection(ConnectionString))
        {
            var results = await connection.QueryAsync<OrganizationUserUserDetails>(
                "[dbo].[OrganizationUserUserDetails_ReadById]",
                new { Id = id },
                commandType: CommandType.StoredProcedure);

            return results.SingleOrDefault();
        }
    }
    public async Task<(OrganizationUserUserDetails? OrganizationUser, ICollection<CollectionAccessSelection> Collections)> GetDetailsByIdWithCollectionsAsync(Guid id)
    {
        using (var connection = new SqlConnection(ConnectionString))
        {
            var results = await connection.QueryMultipleAsync(
                "[dbo].[OrganizationUserUserDetails_ReadWithCollectionsById]",
                new { Id = id },
                commandType: CommandType.StoredProcedure);

            var organizationUserUserDetails = (await results.ReadAsync<OrganizationUserUserDetails>()).SingleOrDefault();
            var collections = (await results.ReadAsync<CollectionAccessSelection>()).ToList();
            return (organizationUserUserDetails, collections);
        }
    }

    public async Task<ICollection<OrganizationUserUserDetails>> GetManyDetailsByOrganizationAsync(Guid organizationId, bool includeGroups, bool includeCollections)
    {
        using (var connection = new SqlConnection(ConnectionString))
        {
            var results = await connection.QueryAsync<OrganizationUserUserDetails>(
                "[dbo].[OrganizationUserUserDetails_ReadByOrganizationId]",
                new { OrganizationId = organizationId },
                commandType: CommandType.StoredProcedure);

            List<IGrouping<Guid, GroupUser>>? userGroups = null;
            List<IGrouping<Guid, CollectionUser>>? userCollections = null;

            var users = results.ToList();

            if (!includeCollections && !includeGroups)
            {
                return users;
            }

            var orgUserIds = users.Select(u => u.Id).ToGuidIdArrayTVP();

            if (includeGroups)
            {
                userGroups = (await connection.QueryAsync<GroupUser>(
                    "[dbo].[GroupUser_ReadByOrganizationUserIds]",
                    new { OrganizationUserIds = orgUserIds },
                    commandType: CommandType.StoredProcedure)).GroupBy(u => u.OrganizationUserId).ToList();
            }

            if (includeCollections)
            {
                userCollections = (await connection.QueryAsync<CollectionUser>(
                    "[dbo].[CollectionUser_ReadByOrganizationUserIds]",
                    new { OrganizationUserIds = orgUserIds },
                    commandType: CommandType.StoredProcedure)).GroupBy(u => u.OrganizationUserId).ToList();
            }

            // Map any queried collections and groups to their respective users
            foreach (var user in users)
            {
                if (userGroups != null)
                {
                    user.Groups = userGroups
                        .FirstOrDefault(u => u.Key == user.Id)?
                        .Select(ug => ug.GroupId).ToList() ?? new List<Guid>();
                }

                if (userCollections != null)
                {
                    user.Collections = userCollections
                        .FirstOrDefault(u => u.Key == user.Id)?
                        .Select(uc => new CollectionAccessSelection
                        {
                            Id = uc.CollectionId,
                            ReadOnly = uc.ReadOnly,
                            HidePasswords = uc.HidePasswords,
                            Manage = uc.Manage
                        }).ToList() ?? new List<CollectionAccessSelection>();
                }
            }

            return users;
        }
    }

    public async Task<ICollection<OrganizationUserOrganizationDetails>> GetManyDetailsByUserAsync(Guid userId,
        OrganizationUserStatusType? status = null)
    {
        using (var connection = new SqlConnection(ConnectionString))
        {
            var results = await connection.QueryAsync<OrganizationUserOrganizationDetails>(
                "[dbo].[OrganizationUserOrganizationDetails_ReadByUserIdStatus]",
                new { UserId = userId, Status = status },
                commandType: CommandType.StoredProcedure);

            return results.ToList();
        }
    }

    public async Task<OrganizationUserOrganizationDetails?> GetDetailsByUserAsync(Guid userId,
        Guid organizationId, OrganizationUserStatusType? status = null)
    {
        using (var connection = new SqlConnection(ConnectionString))
        {
            var results = await connection.QueryAsync<OrganizationUserOrganizationDetails>(
                "[dbo].[OrganizationUserOrganizationDetails_ReadByUserIdStatusOrganizationId]",
                new { UserId = userId, Status = status, OrganizationId = organizationId },
                commandType: CommandType.StoredProcedure);

            return results.SingleOrDefault();
        }
    }

    public async Task UpdateGroupsAsync(Guid orgUserId, IEnumerable<Guid> groupIds)
    {
        using (var connection = new SqlConnection(ConnectionString))
        {
            var results = await connection.ExecuteAsync(
                "[dbo].[GroupUser_UpdateGroups]",
                new { OrganizationUserId = orgUserId, GroupIds = groupIds.ToGuidIdArrayTVP() },
                commandType: CommandType.StoredProcedure);
        }
    }

    public async Task<Guid> CreateAsync(OrganizationUser obj, IEnumerable<CollectionAccessSelection> collections)
    {
        obj.SetNewId();
        var objWithCollections = JsonSerializer.Deserialize<OrganizationUserWithCollections>(
            JsonSerializer.Serialize(obj))!;
        objWithCollections.Collections = collections.ToArrayTVP();

        using (var connection = new SqlConnection(ConnectionString))
        {
            var results = await connection.ExecuteAsync(
                $"[{Schema}].[OrganizationUser_CreateWithCollections]",
                objWithCollections,
                commandType: CommandType.StoredProcedure);
        }

        return obj.Id;
    }

    public async Task ReplaceAsync(OrganizationUser obj, IEnumerable<CollectionAccessSelection> collections)
    {
        var objWithCollections = JsonSerializer.Deserialize<OrganizationUserWithCollections>(
            JsonSerializer.Serialize(obj))!;
        objWithCollections.Collections = collections.ToArrayTVP();

        using (var connection = new SqlConnection(ConnectionString))
        {
            var results = await connection.ExecuteAsync(
                $"[{Schema}].[OrganizationUser_UpdateWithCollections]",
                objWithCollections,
                commandType: CommandType.StoredProcedure);
        }
    }

    public async Task<ICollection<OrganizationUser>> GetManyByManyUsersAsync(IEnumerable<Guid> userIds)
    {
        using (var connection = new SqlConnection(ConnectionString))
        {
            var results = await connection.QueryAsync<OrganizationUser>(
                "[dbo].[OrganizationUser_ReadByUserIds]",
                new { UserIds = userIds.ToGuidIdArrayTVP() },
                commandType: CommandType.StoredProcedure);

            return results.ToList();
        }
    }

    public async Task<ICollection<OrganizationUser>> GetManyAsync(IEnumerable<Guid> Ids)
    {
        using (var connection = new SqlConnection(ConnectionString))
        {
            var results = await connection.QueryAsync<OrganizationUser>(
                "[dbo].[OrganizationUser_ReadByIds]",
                new { Ids = Ids.ToGuidIdArrayTVP() },
                commandType: CommandType.StoredProcedure);

            return results.ToList();
        }
    }

    public async Task<OrganizationUser?> GetByOrganizationEmailAsync(Guid organizationId, string email)
    {
        using (var connection = new SqlConnection(ConnectionString))
        {
            var results = await connection.QueryAsync<OrganizationUser>(
                "[dbo].[OrganizationUser_ReadByOrganizationIdEmail]",
                new { OrganizationId = organizationId, Email = email },
                commandType: CommandType.StoredProcedure);

            return results.SingleOrDefault();
        }
    }

    public async Task DeleteManyAsync(IEnumerable<Guid> organizationUserIds)
    {
        using (var connection = new SqlConnection(ConnectionString))
        {
            await connection.ExecuteAsync("[dbo].[OrganizationUser_DeleteByIds]",
                new { Ids = organizationUserIds.ToGuidIdArrayTVP() }, commandType: CommandType.StoredProcedure);
        }
    }

    public async Task UpsertManyAsync(IEnumerable<OrganizationUser> organizationUsers)
    {
        var createUsers = new List<OrganizationUser>();
        var replaceUsers = new List<OrganizationUser>();
        foreach (var organizationUser in organizationUsers)
        {
            if (organizationUser.Id.Equals(default))
            {
                createUsers.Add(organizationUser);
            }
            else
            {
                replaceUsers.Add(organizationUser);
            }
        }

        await CreateManyAsync(createUsers);
        await ReplaceManyAsync(replaceUsers);
    }

    public async Task<ICollection<Guid>?> CreateManyAsync(IEnumerable<OrganizationUser> organizationUsers)
    {
        organizationUsers = organizationUsers.ToList();
        if (!organizationUsers.Any())
        {
            return default;
        }

        foreach (var organizationUser in organizationUsers)
        {
            organizationUser.SetNewId();
        }

        using (var connection = new SqlConnection(_marsConnectionString))
        {
            var results = await connection.ExecuteAsync(
                $"[{Schema}].[{Table}_CreateMany]",
                new { jsonData = JsonSerializer.Serialize(organizationUsers) },
                commandType: CommandType.StoredProcedure);
        }

        return organizationUsers.Select(u => u.Id).ToList();
    }

    public async Task ReplaceManyAsync(IEnumerable<OrganizationUser> organizationUsers)
    {
        organizationUsers = organizationUsers.ToList();
        if (!organizationUsers.Any())
        {
            return;
        }

        using (var connection = new SqlConnection(_marsConnectionString))
        {
            var results = await connection.ExecuteAsync(
                $"[{Schema}].[{Table}_UpdateMany]",
                new { jsonData = JsonSerializer.Serialize(organizationUsers) },
                commandType: CommandType.StoredProcedure);
        }
    }

    public async Task<IEnumerable<OrganizationUserPublicKey>> GetManyPublicKeysByOrganizationUserAsync(
        Guid organizationId, IEnumerable<Guid> Ids)
    {
        using (var connection = new SqlConnection(ConnectionString))
        {
            var results = await connection.QueryAsync<OrganizationUserPublicKey>(
                "[dbo].[User_ReadPublicKeysByOrganizationUserIds]",
                new { OrganizationId = organizationId, OrganizationUserIds = Ids.ToGuidIdArrayTVP() },
                commandType: CommandType.StoredProcedure);

            return results.ToList();
        }
    }

    public async Task<IEnumerable<OrganizationUserUserDetails>> GetManyByMinimumRoleAsync(Guid organizationId, OrganizationUserType minRole)
    {
        using (var connection = new SqlConnection(ConnectionString))
        {
            var results = await connection.QueryAsync<OrganizationUserUserDetails>(
                "[dbo].[OrganizationUser_ReadByMinimumRole]",
                new { OrganizationId = organizationId, MinRole = minRole },
                commandType: CommandType.StoredProcedure);

            return results.ToList();
        }
    }

    public async Task RevokeAsync(Guid id)
    {
        using (var connection = new SqlConnection(ConnectionString))
        {
            var results = await connection.ExecuteAsync(
                $"[{Schema}].[{Table}_Deactivate]",
                new { Id = id },
                commandType: CommandType.StoredProcedure);
        }
    }

    public async Task RestoreAsync(Guid id, OrganizationUserStatusType status)
    {
        using (var connection = new SqlConnection(ConnectionString))
        {
            var results = await connection.ExecuteAsync(
                $"[{Schema}].[{Table}_Activate]",
                new { Id = id, Status = status },
                commandType: CommandType.StoredProcedure);
        }
    }

    public async Task<IEnumerable<OrganizationUserPolicyDetails>> GetByUserIdWithPolicyDetailsAsync(Guid userId, PolicyType policyType)
    {
        using (var connection = new SqlConnection(ConnectionString))
        {
            var results = await connection.QueryAsync<OrganizationUserPolicyDetails>(
                $"[{Schema}].[{Table}_ReadByUserIdWithPolicyDetails]",
                new { UserId = userId, PolicyType = policyType },
                commandType: CommandType.StoredProcedure);

            return results.ToList();
        }
    }

    public async Task<IEnumerable<OrganizationUserResetPasswordDetails>> GetManyAccountRecoveryDetailsByOrganizationUserAsync(
        Guid organizationId, IEnumerable<Guid> organizationUserIds)
    {
        using (var connection = new SqlConnection(ConnectionString))
        {
            var results = await connection.QueryAsync<OrganizationUserResetPasswordDetails>(
                "[dbo].[OrganizationUser_ReadManyAccountRecoveryDetailsByOrganizationUserIds]",
                new { OrganizationId = organizationId, OrganizationUserIds = organizationUserIds.ToGuidIdArrayTVP() },
                commandType: CommandType.StoredProcedure);

            return results.ToList();
        }
    }

    /// <inheritdoc />
    public UpdateEncryptedDataForKeyRotation UpdateForKeyRotation(
        Guid userId, IEnumerable<OrganizationUser> resetPasswordKeys)
    {
        return async (connection, transaction) =>
            await connection.ExecuteAsync(
                $"[{Schema}].[OrganizationUser_UpdateDataForKeyRotation]",
                new { UserId = userId, OrganizationUserJson = JsonSerializer.Serialize(resetPasswordKeys) },
                transaction: transaction,
                commandType: CommandType.StoredProcedure);
    }

    public async Task<ICollection<OrganizationUser>> GetManyByOrganizationWithClaimedDomainsAsync(Guid organizationId)
    {
        using (var connection = new SqlConnection(ConnectionString))
        {
            var results = await connection.QueryAsync<OrganizationUser>(
                $"[{Schema}].[OrganizationUser_ReadByOrganizationIdWithClaimedDomains]",
                new { OrganizationId = organizationId },
                commandType: CommandType.StoredProcedure);

            return results.ToList();
        }
    }

    public async Task RevokeManyByIdAsync(IEnumerable<Guid> organizationUserIds)
    {
        await using var connection = new SqlConnection(ConnectionString);

        await connection.ExecuteAsync(
            "[dbo].[OrganizationUser_SetStatusForUsersById]",
            new { OrganizationUserIds = JsonSerializer.Serialize(organizationUserIds), Status = OrganizationUserStatusType.Revoked },
            commandType: CommandType.StoredProcedure);
    }
}
