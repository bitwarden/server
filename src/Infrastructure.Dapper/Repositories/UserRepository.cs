using System.Data;
using System.Text.Json;
using Bit.Core;
using Bit.Core.Entities;
using Bit.Core.KeyManagement.UserKey;
using Bit.Core.Models.Data;
using Bit.Core.Repositories;
using Bit.Core.Settings;
using Dapper;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Data.SqlClient;

#nullable enable

namespace Bit.Infrastructure.Dapper.Repositories;

public class UserRepository : Repository<User, Guid>, IUserRepository
{
    private readonly IDataProtector _dataProtector;

    public UserRepository(
        GlobalSettings globalSettings,
        IDataProtectionProvider dataProtectionProvider)
        : base(globalSettings.SqlServer.ConnectionString, globalSettings.SqlServer.ReadOnlyConnectionString)
    {
        _dataProtector = dataProtectionProvider.CreateProtector(Constants.DatabaseFieldProtectorPurpose);
    }

    public override async Task<User?> GetByIdAsync(Guid id)
    {
        var user = await base.GetByIdAsync(id);
        UnprotectData(user);
        return user;
    }

    public async Task<User?> GetByEmailAsync(string email)
    {
        using (var connection = new SqlConnection(ConnectionString))
        {
            var results = await connection.QueryAsync<User>(
                $"[{Schema}].[{Table}_ReadByEmail]",
                new { Email = email },
                commandType: CommandType.StoredProcedure);

            UnprotectData(results);
            return results.SingleOrDefault();
        }
    }

    public async Task<IEnumerable<User>> GetManyByEmailsAsync(IEnumerable<string> emails)
    {
        var emailTable = new DataTable();
        emailTable.Columns.Add("Email", typeof(string));
        foreach (var email in emails)
        {
            emailTable.Rows.Add(email);
        }

        using (var connection = new SqlConnection(ConnectionString))
        {
            var results = await connection.QueryAsync<User>(
                $"[{Schema}].[{Table}_ReadByEmails]",
                new { Emails = emailTable.AsTableValuedParameter("dbo.EmailArray") },
                commandType: CommandType.StoredProcedure);

            UnprotectData(results);
            return results.ToList();
        }
    }

    public async Task<User?> GetBySsoUserAsync(string externalId, Guid? organizationId)
    {
        using (var connection = new SqlConnection(ConnectionString))
        {
            var results = await connection.QueryAsync<User>(
                $"[{Schema}].[{Table}_ReadBySsoUserOrganizationIdExternalId]",
                new { OrganizationId = organizationId, ExternalId = externalId },
                commandType: CommandType.StoredProcedure);

            UnprotectData(results);
            return results.SingleOrDefault();
        }
    }

    public async Task<UserKdfInformation?> GetKdfInformationByEmailAsync(string email)
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

            UnprotectData(results);
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

            UnprotectData(results);
            return results.ToList();
        }
    }

    public async Task<string?> GetPublicKeyAsync(Guid id)
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
        await ProtectDataAndSaveAsync(user, async () => await base.CreateAsync(user));
        return user;
    }

    public override async Task ReplaceAsync(User user)
    {
        await ProtectDataAndSaveAsync(user, async () => await base.ReplaceAsync(user));
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
    public async Task DeleteManyAsync(IEnumerable<User> users)
    {
        var ids = users.Select(user => user.Id);
        using (var connection = new SqlConnection(ConnectionString))
        {
            await connection.ExecuteAsync(
                $"[{Schema}].[{Table}_DeleteByIds]",
                new { Ids = JsonSerializer.Serialize(ids) },
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

    /// <inheritdoc />
    public async Task UpdateUserKeyAndEncryptedDataAsync(
        User user,
        IEnumerable<UpdateEncryptedDataForKeyRotation> updateDataActions)
    {
        await using var connection = new SqlConnection(ConnectionString);
        connection.Open();

        await using var transaction = connection.BeginTransaction();
        try
        {
            // Update user
            await using (var cmd = new SqlCommand("[dbo].[User_UpdateKeys]", connection, transaction))
            {
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.Parameters.Add("@Id", SqlDbType.UniqueIdentifier).Value = user.Id;
                cmd.Parameters.Add("@SecurityStamp", SqlDbType.NVarChar).Value = user.SecurityStamp;
                cmd.Parameters.Add("@Key", SqlDbType.VarChar).Value = user.Key;

                cmd.Parameters.Add("@PrivateKey", SqlDbType.VarChar).Value =
                    string.IsNullOrWhiteSpace(user.PrivateKey) ? DBNull.Value : user.PrivateKey;

                cmd.Parameters.Add("@RevisionDate", SqlDbType.DateTime2).Value = user.RevisionDate;
                cmd.Parameters.Add("@AccountRevisionDate", SqlDbType.DateTime2).Value =
                    user.AccountRevisionDate;
                cmd.Parameters.Add("@LastKeyRotationDate", SqlDbType.DateTime2).Value =
                    user.LastKeyRotationDate;
                cmd.ExecuteNonQuery();
            }

            //  Update re-encrypted data
            foreach (var action in updateDataActions)
            {
                await action(connection, transaction);
            }

            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
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

            UnprotectData(results);
            return results.ToList();
        }
    }

    public async Task<IEnumerable<UserWithCalculatedPremium>> GetManyWithCalculatedPremiumAsync(IEnumerable<Guid> ids)
    {
        using (var connection = new SqlConnection(ReadOnlyConnectionString))
        {
            var results = await connection.QueryAsync<UserWithCalculatedPremium>(
                $"[{Schema}].[{Table}_ReadByIdsWithCalculatedPremium]",
                new { Ids = JsonSerializer.Serialize(ids) },
                commandType: CommandType.StoredProcedure);

            UnprotectData(results);
            return results.ToList();
        }
    }

    private async Task ProtectDataAndSaveAsync(User user, Func<Task> saveTask)
    {
        if (user == null)
        {
            await saveTask();
            return;
        }

        // Capture original values
        var originalMasterPassword = user.MasterPassword;
        var originalKey = user.Key;

        // Protect values
        if (!user.MasterPassword?.StartsWith(Constants.DatabaseFieldProtectedPrefix) ?? false)
        {
            user.MasterPassword = string.Concat(Constants.DatabaseFieldProtectedPrefix,
                _dataProtector.Protect(user.MasterPassword!));
        }

        if (!user.Key?.StartsWith(Constants.DatabaseFieldProtectedPrefix) ?? false)
        {
            user.Key = string.Concat(Constants.DatabaseFieldProtectedPrefix,
                _dataProtector.Protect(user.Key!));
        }

        // Save
        await saveTask();

        // Restore original values
        user.MasterPassword = originalMasterPassword;
        user.Key = originalKey;
    }

    private void UnprotectData(User? user)
    {
        if (user == null)
        {
            return;
        }

        if (user.MasterPassword?.StartsWith(Constants.DatabaseFieldProtectedPrefix) ?? false)
        {
            user.MasterPassword = _dataProtector.Unprotect(
                user.MasterPassword.Substring(Constants.DatabaseFieldProtectedPrefix.Length));
        }

        if (user.Key?.StartsWith(Constants.DatabaseFieldProtectedPrefix) ?? false)
        {
            user.Key = _dataProtector.Unprotect(
                user.Key.Substring(Constants.DatabaseFieldProtectedPrefix.Length));
        }
    }

    private void UnprotectData(IEnumerable<User> users)
    {
        if (users == null)
        {
            return;
        }

        foreach (var user in users)
        {
            UnprotectData(user);
        }
    }
}
