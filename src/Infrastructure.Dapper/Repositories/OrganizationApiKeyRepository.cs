using System.Data;
using Bit.Core;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using Bit.Core.Settings;
using Dapper;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Data.SqlClient;

#nullable enable

namespace Bit.Infrastructure.Dapper.Repositories;

public class OrganizationApiKeyRepository : Repository<OrganizationApiKey, Guid>, IOrganizationApiKeyRepository
{
    private readonly IDataProtector _dataProtector;

    public OrganizationApiKeyRepository(
        GlobalSettings globalSettings,
        IDataProtectionProvider dataProtectionProvider)
        : this(globalSettings.SqlServer.ConnectionString,
               globalSettings.SqlServer.ReadOnlyConnectionString,
               dataProtectionProvider)
    { }

    public OrganizationApiKeyRepository(
        string connectionString,
        string readOnlyConnectionString,
        IDataProtectionProvider dataProtectionProvider)
        : base(connectionString, readOnlyConnectionString)
    {
        _dataProtector = dataProtectionProvider.CreateProtector(Constants.DatabaseFieldProtectorPurpose);
    }

    public override async Task<OrganizationApiKey?> GetByIdAsync(Guid id)
    {
        var apiKey = await base.GetByIdAsync(id);
        UnprotectData(apiKey);
        return apiKey;
    }

    public override async Task<OrganizationApiKey> CreateAsync(OrganizationApiKey apiKey)
    {
        await ProtectDataAndSaveAsync(apiKey, () => base.CreateAsync(apiKey));
        return apiKey;
    }

    public override async Task ReplaceAsync(OrganizationApiKey apiKey)
    {
        // Also covers UpsertAsync — the base implementation routes to CreateAsync/ReplaceAsync.
        await ProtectDataAndSaveAsync(apiKey, () => base.ReplaceAsync(apiKey));
    }

    public async Task<IEnumerable<OrganizationApiKey>> GetManyByOrganizationIdTypeAsync(Guid organizationId, OrganizationApiKeyType? type = null)
    {
        using (var connection = new SqlConnection(ConnectionString))
        {
            var results = await connection.QueryAsync<OrganizationApiKey>(
                "[dbo].[OrganizationApikey_ReadManyByOrganizationIdType]",
                new
                {
                    OrganizationId = organizationId,
                    Type = type,
                },
                commandType: CommandType.StoredProcedure);
            var apiKeys = results.ToList();
            foreach (var apiKey in apiKeys)
            {
                UnprotectData(apiKey);
            }
            return apiKeys;
        }
    }

    private async Task ProtectDataAndSaveAsync(OrganizationApiKey apiKey, Func<Task> saveTask)
    {
        var originalApiKey = apiKey.ApiKey;
        ProtectData(apiKey);
        try
        {
            await saveTask();
        }
        finally
        {
            // Restore the in-memory value: callers return this same instance to the API response.
            apiKey.ApiKey = originalApiKey;
        }
    }

    private void ProtectData(OrganizationApiKey apiKey)
    {
        if (!apiKey.ApiKey?.StartsWith(Constants.DatabaseFieldProtectedPrefix) ?? false)
        {
            apiKey.ApiKey = string.Concat(Constants.DatabaseFieldProtectedPrefix,
                _dataProtector.Protect(apiKey.ApiKey!));
        }
    }

    private void UnprotectData(OrganizationApiKey? apiKey)
    {
        if (apiKey?.ApiKey?.StartsWith(Constants.DatabaseFieldProtectedPrefix) ?? false)
        {
            apiKey.ApiKey = _dataProtector.Unprotect(
                apiKey.ApiKey.Substring(Constants.DatabaseFieldProtectedPrefix.Length));
        }
    }
}
