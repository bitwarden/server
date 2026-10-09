using System.Data.Common;
using System.Security.Cryptography;
using System.Text;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using Bit.Infrastructure.EntityFramework.Repositories;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Bit.Infrastructure.IntegrationTest.AdminConsole.Repositories;

public class OrganizationScopedApiKeyRepositoryTests
{
    [Theory, DatabaseData]
    public async Task CreateAsync_GetByIdAsync_ReturnsStoredKey(
        IOrganizationScopedApiKeyRepository sut,
        IOrganizationRepository organizationRepository)
    {
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var expireAt = new DateTime(2027, 1, 2, 3, 4, 5, DateTimeKind.Utc);
        var creationDate = new DateTime(2026, 10, 9, 1, 2, 3, DateTimeKind.Utc);
        var created = await sut.CreateAsync(new OrganizationScopedApiKey
        {
            OrganizationId = organization.Id,
            Name = "Directory sync",
            ClientSecretHash = HashSecret("secret"),
            Scopes = "[\"api.organization.members.read\",\"api.organization.groups.read\"]",
            ExpireAt = expireAt,
            CreationDate = creationDate,
            RevisionDate = creationDate,
        });

        var result = await sut.GetByIdAsync(created.Id);

        Assert.NotNull(result);
        Assert.NotEqual(Guid.Empty, result.Id);
        Assert.Equal(organization.Id, result.OrganizationId);
        Assert.Equal("Directory sync", result.Name);
        Assert.Equal(HashSecret("secret"), result.ClientSecretHash);
        Assert.Equal(["api.organization.members.read", "api.organization.groups.read"], result.GetScopes());
        Assert.Equal(expireAt, result.ExpireAt);
        Assert.Equal(creationDate, result.CreationDate);
        Assert.Equal(creationDate, result.RevisionDate);
    }

    [Theory, DatabaseData]
    public async Task CreateAsync_WithoutExpiration_StoresNullExpireAt(
        IOrganizationScopedApiKeyRepository sut,
        IOrganizationRepository organizationRepository)
    {
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var created = await CreateKeyAsync(sut, organization.Id);

        var result = await sut.GetByIdAsync(created.Id);

        Assert.NotNull(result);
        Assert.Null(result.ExpireAt);
    }

    [Theory, DatabaseData]
    public async Task GetByIdAsync_UnknownId_ReturnsNull(IOrganizationScopedApiKeyRepository sut)
    {
        var result = await sut.GetByIdAsync(Guid.NewGuid());

        Assert.Null(result);
    }

    [Theory, DatabaseData]
    public async Task GetManyByOrganizationIdAsync_ReturnsOnlyThatOrganizationsKeys(
        IOrganizationScopedApiKeyRepository sut,
        IOrganizationRepository organizationRepository)
    {
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var otherOrganization = await organizationRepository.CreateTestOrganizationAsync();
        var first = await CreateKeyAsync(sut, organization.Id);
        var second = await CreateKeyAsync(sut, organization.Id);
        await CreateKeyAsync(sut, otherOrganization.Id);

        var result = await sut.GetManyByOrganizationIdAsync(organization.Id);

        Assert.Equal(
            new[] { first.Id, second.Id }.Order(),
            result.Select(k => k.Id).Order());
    }

    [Theory, DatabaseData]
    public async Task DeleteAsync_RemovesOnlyThatKey(
        IOrganizationScopedApiKeyRepository sut,
        IOrganizationRepository organizationRepository)
    {
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var deleted = await CreateKeyAsync(sut, organization.Id);
        var kept = await CreateKeyAsync(sut, organization.Id);

        await sut.DeleteAsync(deleted);

        Assert.Null(await sut.GetByIdAsync(deleted.Id));
        Assert.NotNull(await sut.GetByIdAsync(kept.Id));
    }

    [Theory, DatabaseData]
    public async Task OrganizationDeleteAsync_RemovesItsKeys(
        IOrganizationScopedApiKeyRepository sut,
        IOrganizationRepository organizationRepository)
    {
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        await CreateKeyAsync(sut, organization.Id);
        await CreateKeyAsync(sut, organization.Id);

        await organizationRepository.DeleteAsync(organization);

        Assert.Empty(await sut.GetManyByOrganizationIdAsync(organization.Id));
    }

    [Theory, DatabaseData]
    public async Task OrganizationDeleteAsync_KeepsOtherOrganizationsKeys(
        IOrganizationScopedApiKeyRepository sut,
        IOrganizationRepository organizationRepository)
    {
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var otherOrganization = await organizationRepository.CreateTestOrganizationAsync();
        await CreateKeyAsync(sut, organization.Id);
        var otherKey = await CreateKeyAsync(sut, otherOrganization.Id);

        await organizationRepository.DeleteAsync(organization);

        Assert.NotNull(await sut.GetByIdAsync(otherKey.Id));
    }

    [Theory, DatabaseData]
    public async Task CreateAsync_StoredRow_HoldsHashAndNotSecret(
        IOrganizationScopedApiKeyRepository sut,
        IOrganizationRepository organizationRepository,
        Database database,
        IServiceProvider services)
    {
        const string secret = "plaintext-client-secret";
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var name = $"key-{Guid.NewGuid()}";
        await sut.CreateAsync(new OrganizationScopedApiKey
        {
            OrganizationId = organization.Id,
            Name = name,
            ClientSecretHash = HashSecret(secret),
            Scopes = "[\"api.organization.events.read\"]",
        });

        var row = await ReadRawRowByNameAsync(services, database, name);

        Assert.Equal(
            new[] { "ClientSecretHash", "CreationDate", "ExpireAt", "Id", "Name", "OrganizationId", "RevisionDate", "Scopes" },
            row.Keys.Order(StringComparer.Ordinal));
        Assert.Equal(HashSecret(secret), row["ClientSecretHash"]);
        Assert.DoesNotContain(row.Values, value => value?.ToString()?.Contains(secret) ?? false);
    }

    private static Task<OrganizationScopedApiKey> CreateKeyAsync(
        IOrganizationScopedApiKeyRepository repository, Guid organizationId) =>
        repository.CreateAsync(new OrganizationScopedApiKey
        {
            OrganizationId = organizationId,
            Name = "Test key",
            ClientSecretHash = HashSecret(Guid.NewGuid().ToString()),
            Scopes = "[\"api.organization.members.read\"]",
        });

    private static string HashSecret(string secret) =>
        Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(secret)));

    /// <summary>
    /// Reads every column of the stored row, bypassing the repository and entity mapping.
    /// </summary>
    private static async Task<Dictionary<string, object?>> ReadRawRowByNameAsync(
        IServiceProvider services, Database database, string name)
    {
        if (database.Type == SupportedDatabaseProviders.SqlServer && !database.UseEf)
        {
            await using var connection = new SqlConnection(database.ConnectionString);
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT * FROM [dbo].[OrganizationScopedApiKey] WHERE [Name] = @Name";
            command.Parameters.AddWithValue("@Name", name);
            return await ReadSingleRowAsync(command);
        }

        using var scope = services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DatabaseContext>();
        var sql = dbContext.GetService<ISqlGenerationHelper>();
        var efConnection = dbContext.Database.GetDbConnection();
        await efConnection.OpenAsync();
        await using var efCommand = efConnection.CreateCommand();
        efCommand.CommandText =
            $"SELECT * FROM {sql.DelimitIdentifier("OrganizationScopedApiKey")} WHERE {sql.DelimitIdentifier("Name")} = @name";
        var parameter = efCommand.CreateParameter();
        parameter.ParameterName = "@name";
        parameter.Value = name;
        efCommand.Parameters.Add(parameter);
        return await ReadSingleRowAsync(efCommand);
    }

    private static async Task<Dictionary<string, object?>> ReadSingleRowAsync(DbCommand command)
    {
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        var row = new Dictionary<string, object?>();
        for (var i = 0; i < reader.FieldCount; i++)
        {
            row[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
        }
        Assert.False(await reader.ReadAsync());
        return row;
    }
}
