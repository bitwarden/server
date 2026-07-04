using System.Data;
using Bit.Core.Settings;
using Bit.Infrastructure.Dapper.Repositories;
using Bit.Pam.Entities;
using Bit.Pam.Models;
using Bit.Pam.Repositories;
using Dapper;
using Microsoft.Data.SqlClient;

#nullable enable

namespace Bit.Infrastructure.Dapper.Pam.Repositories;

public class AccessRuleRepository : Repository<AccessRule, Guid>, IAccessRuleRepository
{
    public AccessRuleRepository(GlobalSettings globalSettings)
        : this(globalSettings.SqlServer.ConnectionString, globalSettings.SqlServer.ReadOnlyConnectionString)
    { }

    public AccessRuleRepository(string connectionString, string readOnlyConnectionString)
        : base(connectionString, readOnlyConnectionString)
    { }

    public async Task<ICollection<AccessRule>> GetManyByOrganizationIdAsync(Guid organizationId)
    {
        using var connection = new SqlConnection(ConnectionString);
        var results = await connection.QueryAsync<AccessRule>(
            $"[{Schema}].[AccessRule_ReadByOrganizationId]",
            new { OrganizationId = organizationId },
            commandType: CommandType.StoredProcedure);

        return results.ToList();
    }

    public async Task<AccessRuleDetails?> GetDetailsByIdAsync(Guid id)
    {
        using var connection = new SqlConnection(ConnectionString);
        using var results = await connection.QueryMultipleAsync(
            $"[{Schema}].[AccessRule_ReadDetailsById]",
            new { Id = id },
            commandType: CommandType.StoredProcedure);

        var rule = (await results.ReadAsync<AccessRuleDetails>()).SingleOrDefault();
        if (rule is null)
        {
            return null;
        }

        rule.CollectionIds = (await results.ReadAsync<Guid>()).ToList();
        return rule;
    }

    public async Task<ICollection<AccessRuleDetails>> GetManyDetailsByOrganizationIdAsync(Guid organizationId)
    {
        using var connection = new SqlConnection(ConnectionString);
        using var results = await connection.QueryMultipleAsync(
            $"[{Schema}].[AccessRule_ReadDetailsByOrganizationId]",
            new { OrganizationId = organizationId },
            commandType: CommandType.StoredProcedure);

        var rules = (await results.ReadAsync<AccessRuleDetails>()).ToList();
        var collectionIdsByRule = (await results.ReadAsync<CollectionAccessRuleMapping>())
            .GroupBy(m => m.AccessRuleId)
            .ToDictionary(g => g.Key, g => g.Select(m => m.CollectionId).ToList());

        foreach (var rule in rules)
        {
            if (collectionIdsByRule.TryGetValue(rule.Id, out var collectionIds))
            {
                rule.CollectionIds = collectionIds;
            }
        }

        return rules;
    }

    public async Task SetCollectionAssociationsAsync(Guid organizationId, Guid accessRuleId,
        IEnumerable<Guid> collectionIdsToAssign, IEnumerable<Guid> collectionIdsToClear)
    {
        using var connection = new SqlConnection(ConnectionString);
        await connection.ExecuteAsync(
            $"[{Schema}].[Collection_SetAccessRuleAssociations]",
            new
            {
                AccessRuleId = accessRuleId,
                OrganizationId = organizationId,
                ToAssign = collectionIdsToAssign.ToGuidIdArrayTVP(),
                ToClear = collectionIdsToClear.ToGuidIdArrayTVP(),
            },
            commandType: CommandType.StoredProcedure);
    }

    private sealed class CollectionAccessRuleMapping
    {
        public Guid AccessRuleId { get; init; }
        public Guid CollectionId { get; init; }
    }
}
