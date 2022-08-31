using System.Data;
using System.Data.SqlClient;
using System.Text.Json;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Models.Data;
using Bit.Core.Models.Data.Organizations.OrganizationUsers;
using Bit.Core.Repositories;
using Bit.Core.Settings;
using Dapper;

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
        : this(globalSettings.SqlServer.ConnectionString, globalSettings.SqlServer.ReadOnlyConnectionString)
    {
        var builder = new SqlConnectionStringBuilder(ConnectionString)
        {
            MultipleActiveResultSets = true,
        };
        _marsConnectionString = builder.ToString();
    }

    public OrganizationUserRepository(string connectionString, string readOnlyConnectionString)
        : base(connectionString, readOnlyConnectionString)
    { }

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

    public async Task<OrganizationUser> GetByOrganizationAsync(Guid organizationId, Guid userId)
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

    public async Task<Tuple<OrganizationUser, ICollection<SelectionReadOnly>>> GetByIdWithCollectionsAsync(Guid id)
    {
        using (var connection = new SqlConnection(ConnectionString))
        {
            var results = await connection.QueryMultipleAsync(
                "[dbo].[OrganizationUser_ReadWithCollectionsById]",
                new { Id = id },
                commandType: CommandType.StoredProcedure);

            var user = (await results.ReadAsync<OrganizationUser>()).SingleOrDefault();
            var collections = (await results.ReadAsync<SelectionReadOnly>()).ToList();
            return new Tuple<OrganizationUser, ICollection<SelectionReadOnly>>(user, collections);
        }
    }

    public async Task<OrganizationUserUserDetails> GetDetailsByIdAsync(Guid id)
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
    public async Task<Tuple<OrganizationUserUserDetails, ICollection<SelectionReadOnly>>>
        GetDetailsByIdWithCollectionsAsync(Guid id)
    {
        using (var connection = new SqlConnection(ConnectionString))
        {
            var results = await connection.QueryMultipleAsync(
                "[dbo].[OrganizationUserUserDetails_ReadWithCollectionsById]",
                new { Id = id },
                commandType: CommandType.StoredProcedure);

            var user = (await results.ReadAsync<OrganizationUserUserDetails>()).SingleOrDefault();
            var collections = (await results.ReadAsync<SelectionReadOnly>()).ToList();
            return new Tuple<OrganizationUserUserDetails, ICollection<SelectionReadOnly>>(user, collections);
        }
    }

    public async Task<ICollection<OrganizationUserUserDetails>> GetManyDetailsByOrganizationAsync(Guid organizationId)
    {
        using (var connection = new SqlConnection(ConnectionString))
        {
            var results = await connection.QueryAsync<OrganizationUserUserDetails>(
                "[dbo].[OrganizationUserUserDetails_ReadByOrganizationId]",
                new { OrganizationId = organizationId },
                commandType: CommandType.StoredProcedure);

            return results.ToList();
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

    public async Task<OrganizationUserOrganizationDetails> GetDetailsByUserAsync(Guid userId,
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

    public async Task<Guid> CreateAsync(OrganizationUser obj, IEnumerable<SelectionReadOnly> collections)
    {
        obj.SetNewId();
        var objWithCollections = JsonSerializer.Deserialize<OrganizationUserWithCollections>(
            JsonSerializer.Serialize(obj));
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

    public async Task ReplaceAsync(OrganizationUser obj, IEnumerable<SelectionReadOnly> collections)
    {
        var objWithCollections = JsonSerializer.Deserialize<OrganizationUserWithCollections>(
            JsonSerializer.Serialize(obj));
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

    public async Task<OrganizationUser> GetByOrganizationEmailAsync(Guid organizationId, string email)
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

    public async Task<ICollection<Guid>> CreateManyAsync(IEnumerable<OrganizationUser> organizationUsers)
    {
        if (!organizationUsers.Any())
        {
            return default;
        }

        foreach (var organizationUser in organizationUsers)
        {
            organizationUser.SetNewId();
        }

        var orgUsersTVP = organizationUsers.ToTvp();
        using (var connection = new SqlConnection(_marsConnectionString))
        {
            var results = await connection.ExecuteAsync(
                $"[{Schema}].[{Table}_CreateMany]",
                new { OrganizationUsersInput = orgUsersTVP },
                commandType: CommandType.StoredProcedure);
        }

        return organizationUsers.Select(u => u.Id).ToList();
    }

    public async Task ReplaceManyAsync(IEnumerable<OrganizationUser> organizationUsers)
    {
        if (!organizationUsers.Any())
        {
            return;
        }

        var orgUsersTVP = organizationUsers.ToTvp();
        using (var connection = new SqlConnection(_marsConnectionString))
        {
            var results = await connection.ExecuteAsync(
                $"[{Schema}].[{Table}_UpdateMany]",
                new { OrganizationUsersInput = orgUsersTVP },
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
}
