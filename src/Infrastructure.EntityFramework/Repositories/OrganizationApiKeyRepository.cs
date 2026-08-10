using AutoMapper;
using Bit.Core;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

#nullable enable

namespace Bit.Infrastructure.EntityFramework.Repositories;

// Protection is repository-level (the OrganizationInviteLink pattern) rather than a value
// converter: Data Protection output is non-deterministic, so a converter would poison LINQ
// predicates over the column — including the protection migration's own keyset read and
// compare-and-swap write.
public class OrganizationApiKeyRepository : Repository<OrganizationApiKey, Models.OrganizationApiKey, Guid>, IOrganizationApiKeyRepository
{
    private readonly IDataProtector _dataProtector;

    public OrganizationApiKeyRepository(IServiceScopeFactory serviceScopeFactory, IMapper mapper,
        IDataProtectionProvider dataProtectionProvider)
        : base(serviceScopeFactory, mapper, db => db.OrganizationApiKeys)
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
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            var apiKeys = await dbContext.OrganizationApiKeys
                .Where(o => o.OrganizationId == organizationId && (type == null || o.Type == type))
                .ToListAsync();
            var mapped = Mapper.Map<List<OrganizationApiKey>>(apiKeys);
            foreach (var apiKey in mapped)
            {
                UnprotectData(apiKey);
            }
            return mapped;
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
