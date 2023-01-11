using System.Data;
using System.Security.Cryptography;
using System.Text;
using Bit.Core.Entities;
using Bit.Core.Models.Data;
using Bit.Core.Repositories;
using Bit.Core.Settings;
using Dapper;
using Microsoft.Data.SqlClient;

namespace Bit.Infrastructure.Dapper.Repositories;

public class UserRepository : Repository<User, Guid>, IUserRepository
{
    public UserRepository(GlobalSettings globalSettings)
        : this(globalSettings.SqlServer.ConnectionString, globalSettings.SqlServer.ReadOnlyConnectionString)
    { }

    public UserRepository(string connectionString, string readOnlyConnectionString)
        : base(connectionString, readOnlyConnectionString)
    { }

    public override async Task<User> GetByIdAsync(Guid id)
    {
        var user = await base.GetByIdAsync(id);
        await UnprotectDataAsync(user);
        return user;
    }

    public async Task<User> GetByEmailAsync(string email)
    {
        using (var connection = new SqlConnection(ConnectionString))
        {
            var results = await connection.QueryAsync<User>(
                $"[{Schema}].[{Table}_ReadByEmail]",
                new { Email = email },
                commandType: CommandType.StoredProcedure);

            await UnprotectDataAsync(results);
            return results.SingleOrDefault();
        }
    }

    public async Task<User> GetBySsoUserAsync(string externalId, Guid? organizationId)
    {
        using (var connection = new SqlConnection(ConnectionString))
        {
            var results = await connection.QueryAsync<User>(
                $"[{Schema}].[{Table}_ReadBySsoUserOrganizationIdExternalId]",
                new { OrganizationId = organizationId, ExternalId = externalId },
                commandType: CommandType.StoredProcedure);

            await UnprotectDataAsync(results);
            return results.SingleOrDefault();
        }
    }

    public async Task<UserKdfInformation> GetKdfInformationByEmailAsync(string email)
    {
        using (var connection = new SqlConnection(ConnectionString))
        {
            var results = await connection.QueryAsync<UserKdfInformation>(
                $"[{Schema}].[{Table}_ReadKdfByEmail]",
                new { Email = email },
                commandType: CommandType.StoredProcedure);

            return results.SingleOrDefault();
        }
    }

    public async Task<ICollection<User>> SearchAsync(string email, int skip, int take)
    {
        using (var connection = new SqlConnection(ReadOnlyConnectionString))
        {
            var results = await connection.QueryAsync<User>(
                $"[{Schema}].[{Table}_Search]",
                new { Email = email, Skip = skip, Take = take },
                commandType: CommandType.StoredProcedure,
                commandTimeout: 120);

            await UnprotectDataAsync(results);
            return results.ToList();
        }
    }

    public async Task<ICollection<User>> GetManyByPremiumAsync(bool premium)
    {
        using (var connection = new SqlConnection(ConnectionString))
        {
            var results = await connection.QueryAsync<User>(
                "[dbo].[User_ReadByPremium]",
                new { Premium = premium },
                commandType: CommandType.StoredProcedure);

            await UnprotectDataAsync(results);
            return results.ToList();
        }
    }

    public async Task<string> GetPublicKeyAsync(Guid id)
    {
        using (var connection = new SqlConnection(ConnectionString))
        {
            var results = await connection.QueryAsync<string>(
                $"[{Schema}].[{Table}_ReadPublicKeyById]",
                new { Id = id },
                commandType: CommandType.StoredProcedure);

            return results.SingleOrDefault();
        }
    }

    public async Task<DateTime> GetAccountRevisionDateAsync(Guid id)
    {
        using (var connection = new SqlConnection(ReadOnlyConnectionString))
        {
            var results = await connection.QueryAsync<DateTime>(
                $"[{Schema}].[{Table}_ReadAccountRevisionDateById]",
                new { Id = id },
                commandType: CommandType.StoredProcedure);

            return results.SingleOrDefault();
        }
    }
    public override async Task<User> CreateAsync(User user)
    {
        await ProtectDataAsync(user);
        return await base.CreateAsync(user);
    }

    public override async Task ReplaceAsync(User user)
    {
        await ProtectDataAsync(user);
        await base.ReplaceAsync(user);
    }

    public override async Task DeleteAsync(User user)
    {
        using (var connection = new SqlConnection(ConnectionString))
        {
            await connection.ExecuteAsync(
                $"[{Schema}].[{Table}_DeleteById]",
                new { Id = user.Id },
                commandType: CommandType.StoredProcedure,
                commandTimeout: 180);
        }
    }

    public async Task UpdateStorageAsync(Guid id)
    {
        using (var connection = new SqlConnection(ConnectionString))
        {
            await connection.ExecuteAsync(
                $"[{Schema}].[{Table}_UpdateStorage]",
                new { Id = id },
                commandType: CommandType.StoredProcedure,
                commandTimeout: 180);
        }
    }

    public async Task UpdateRenewalReminderDateAsync(Guid id, DateTime renewalReminderDate)
    {
        using (var connection = new SqlConnection(ConnectionString))
        {
            await connection.ExecuteAsync(
                $"[{Schema}].[User_UpdateRenewalReminderDate]",
                new { Id = id, RenewalReminderDate = renewalReminderDate },
                commandType: CommandType.StoredProcedure);
        }
    }

    public async Task<IEnumerable<User>> GetManyAsync(IEnumerable<Guid> ids)
    {
        using (var connection = new SqlConnection(ReadOnlyConnectionString))
        {
            var results = await connection.QueryAsync<User>(
                $"[{Schema}].[{Table}_ReadByIds]",
                new { Ids = ids.ToGuidIdArrayTVP() },
                commandType: CommandType.StoredProcedure);

            await UnprotectDataAsync(results);
            return results.ToList();
        }
    }

    private async Task ProtectDataAsync(User user)
    {
        var masterPasswordData = new ServerProtectedData { PlaintextData = user.MasterPassword };
        user.MasterPassword = await masterPasswordData.EncryptAsync();

        var keyData = new ServerProtectedData { PlaintextData = user.Key };
        user.Key = await keyData.EncryptAsync();
    }

    private async Task UnprotectDataAsync(User user)
    {
        var masterPasswordData = new ServerProtectedData { EncryptedData = user.MasterPassword };
        if (masterPasswordData.Protected())
        {
            user.MasterPassword = await masterPasswordData.DecryptAsync();
        }

        var keyData = new ServerProtectedData { EncryptedData = user.Key };
        if (keyData.Protected())
        {
            user.Key = await keyData.DecryptAsync();
        }
    }
    private async Task UnprotectDataAsync(IEnumerable<User> users)
    {
        foreach (var user in users)
        {
            await UnprotectDataAsync(user);
        }
    }
}
