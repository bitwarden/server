using System.Data.Common;
using System.Security.Cryptography;
using System.Text;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using Bit.Core.SecretsManager.Entities;
using Bit.Core.SecretsManager.Models.Data;
using Bit.Core.SecretsManager.Repositories;
using Bit.Infrastructure.EntityFramework.Repositories;
using Bit.Infrastructure.IntegrationTest.AdminConsole;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Bit.Infrastructure.IntegrationTest.SecretsManager.Repositories;

public class ApiKeyRepositoryTests
{
    [Theory, DatabaseData]
    public async Task CreateAsync_OrganizationKey_GetByIdAsync_ReturnsStoredKey(
        IApiKeyRepository sut,
        IOrganizationRepository organizationRepository)
    {
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var expireAt = new DateTime(2027, 1, 2, 3, 4, 5, DateTimeKind.Utc);
        var created = await sut.CreateAsync(new ApiKey
        {
            OrganizationId = organization.Id,
            Name = "Directory sync",
            ClientSecretHash = HashSecret("secret"),
            Scope = "[\"api.organization.members.read\",\"api.organization.groups.read\"]",
            ExpireAt = expireAt,
        });

        var result = await sut.GetByIdAsync(created.Id);

        Assert.NotNull(result);
        Assert.Equal(organization.Id, result.OrganizationId);
        Assert.Null(result.ServiceAccountId);
        Assert.Null(result.EncryptedPayload);
        Assert.Null(result.Key);
        Assert.Equal("Directory sync", result.Name);
        Assert.Equal(HashSecret("secret"), result.ClientSecretHash);
        Assert.Equal(["api.organization.members.read", "api.organization.groups.read"], result.GetScopes());
        Assert.Equal(expireAt, result.ExpireAt);
    }

    [Theory, DatabaseData]
    public async Task GetByIdAsync_UnknownId_ReturnsNull(IApiKeyRepository sut)
    {
        var result = await sut.GetByIdAsync(Guid.NewGuid());

        Assert.Null(result);
    }

    [Theory, DatabaseData]
    public async Task GetManyByOrganizationIdAsync_ReturnsOnlyThatOrganizationsKeys(
        IApiKeyRepository sut,
        IOrganizationRepository organizationRepository)
    {
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var otherOrganization = await organizationRepository.CreateTestOrganizationAsync();
        var first = await CreateOrganizationKeyAsync(sut, organization.Id);
        var second = await CreateOrganizationKeyAsync(sut, organization.Id);
        await CreateOrganizationKeyAsync(sut, otherOrganization.Id);

        var result = await sut.GetManyByOrganizationIdAsync(organization.Id);

        Assert.Equal(
            new[] { first.Id, second.Id }.Order(),
            result.Select(k => k.Id).Order());
    }

    [Theory, DatabaseData]
    public async Task GetManyByOrganizationIdAsync_ExcludesServiceAccountKeys(
        IApiKeyRepository sut,
        IOrganizationRepository organizationRepository,
        Database database,
        IServiceProvider services)
    {
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var serviceAccountId = await CreateServiceAccountAsync(services, database, organization.Id);
        await CreateServiceAccountKeyAsync(sut, serviceAccountId);

        var result = await sut.GetManyByOrganizationIdAsync(organization.Id);

        Assert.Empty(result);
    }

    [Theory, DatabaseData]
    public async Task GetDetailsByIdAsync_OrganizationKey_ReturnsNull(
        IApiKeyRepository sut,
        IOrganizationRepository organizationRepository)
    {
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var key = await CreateOrganizationKeyAsync(sut, organization.Id);

        var result = await sut.GetDetailsByIdAsync(key.Id);

        Assert.Null(result);
    }

    [Theory, DatabaseData]
    public async Task GetDetailsByIdAsync_ServiceAccountKey_ReturnsServiceAccountDetails(
        IApiKeyRepository sut,
        IOrganizationRepository organizationRepository,
        Database database,
        IServiceProvider services)
    {
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var serviceAccountId = await CreateServiceAccountAsync(services, database, organization.Id);
        var key = await CreateServiceAccountKeyAsync(sut, serviceAccountId);

        var result = await sut.GetDetailsByIdAsync(key.Id);

        var details = Assert.IsType<ServiceAccountApiKeyDetails>(result);
        Assert.Equal(serviceAccountId, details.ServiceAccountId);
        Assert.Equal(organization.Id, details.ServiceAccountOrganizationId);
        Assert.Null(details.OrganizationId);
    }

    [Theory, DatabaseData]
    public async Task DeleteAsync_RemovesOnlyThatKey(
        IApiKeyRepository sut,
        IOrganizationRepository organizationRepository)
    {
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var deleted = await CreateOrganizationKeyAsync(sut, organization.Id);
        var kept = await CreateOrganizationKeyAsync(sut, organization.Id);

        await sut.DeleteAsync(deleted);

        Assert.Null(await sut.GetByIdAsync(deleted.Id));
        Assert.NotNull(await sut.GetByIdAsync(kept.Id));
    }

    [Theory, DatabaseData]
    public async Task OrganizationDeleteAsync_RemovesItsOrganizationKeys(
        IApiKeyRepository sut,
        IOrganizationRepository organizationRepository)
    {
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var first = await CreateOrganizationKeyAsync(sut, organization.Id);
        var second = await CreateOrganizationKeyAsync(sut, organization.Id);

        await organizationRepository.DeleteAsync(organization);

        Assert.Null(await sut.GetByIdAsync(first.Id));
        Assert.Null(await sut.GetByIdAsync(second.Id));
    }

    [Theory, DatabaseData]
    public async Task OrganizationDeleteAsync_KeepsOtherOrganizationsKeys(
        IApiKeyRepository sut,
        IOrganizationRepository organizationRepository,
        Database database,
        IServiceProvider services)
    {
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var otherOrganization = await organizationRepository.CreateTestOrganizationAsync();
        await CreateOrganizationKeyAsync(sut, organization.Id);
        var otherKey = await CreateOrganizationKeyAsync(sut, otherOrganization.Id);
        var otherServiceAccountId = await CreateServiceAccountAsync(services, database, otherOrganization.Id);
        var otherServiceAccountKey = await CreateServiceAccountKeyAsync(sut, otherServiceAccountId);

        await organizationRepository.DeleteAsync(organization);

        Assert.NotNull(await sut.GetByIdAsync(otherKey.Id));
        Assert.NotNull(await sut.GetByIdAsync(otherServiceAccountKey.Id));
    }

    [Theory, DatabaseData]
    public async Task CreateAsync_OrganizationKey_StoredRowHoldsHashAndNotSecret(
        IApiKeyRepository sut,
        IOrganizationRepository organizationRepository,
        Database database,
        IServiceProvider services)
    {
        const string secret = "plaintext-client-secret";
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var name = $"key-{Guid.NewGuid()}";
        await sut.CreateAsync(new ApiKey
        {
            OrganizationId = organization.Id,
            Name = name,
            ClientSecretHash = HashSecret(secret),
            Scope = "[\"api.organization.events.read\"]",
        });

        var row = await ReadRawRowByNameAsync(services, database, name);

        Assert.Equal(HashSecret(secret), row["ClientSecretHash"]);
        Assert.Null(row["EncryptedPayload"]);
        Assert.Null(row["Key"]);
        Assert.DoesNotContain(row.Values, value => value?.ToString()?.Contains(secret) ?? false);
    }

    [Theory, DatabaseData]
    public async Task CreateAsync_OrganizationKeyWithNonAsciiName_GetByIdAsync_ReturnsSameName(
        IApiKeyRepository sut,
        IOrganizationRepository organizationRepository)
    {
        const string name = "Интеграция 日本 Intégration";
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var created = await sut.CreateAsync(new ApiKey
        {
            OrganizationId = organization.Id,
            Name = name,
            ClientSecretHash = HashSecret("secret"),
            Scope = "[\"api.organization.members.read\"]",
        });

        var result = await sut.GetByIdAsync(created.Id);

        Assert.NotNull(result);
        Assert.Equal(name, result.Name);
    }

    [Theory, DatabaseData]
    public async Task CreateAsync_BothServiceAccountAndOrganization_Throws(
        IApiKeyRepository sut,
        IOrganizationRepository organizationRepository,
        Database database,
        IServiceProvider services)
    {
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var serviceAccountId = await CreateServiceAccountAsync(services, database, organization.Id);

        var exception = await Assert.ThrowsAnyAsync<Exception>(() => sut.CreateAsync(new ApiKey
        {
            ServiceAccountId = serviceAccountId,
            OrganizationId = organization.Id,
            Name = "Two owners",
            ClientSecretHash = HashSecret(Guid.NewGuid().ToString()),
            Scope = "[\"api.secrets\"]",
            EncryptedPayload = "2.payload|iv|mac",
            Key = "2.key|iv|mac",
        }));

        Assert.Contains("CK_ApiKey_SingleOwner", exception.ToString());
    }

    private static Task<ApiKey> CreateOrganizationKeyAsync(IApiKeyRepository repository, Guid organizationId) =>
        repository.CreateAsync(new ApiKey
        {
            OrganizationId = organizationId,
            Name = "Test key",
            ClientSecretHash = HashSecret(Guid.NewGuid().ToString()),
            Scope = "[\"api.organization.members.read\"]",
        });

    private static Task<ApiKey> CreateServiceAccountKeyAsync(IApiKeyRepository repository, Guid serviceAccountId) =>
        repository.CreateAsync(new ApiKey
        {
            ServiceAccountId = serviceAccountId,
            Name = "Machine account token",
            ClientSecretHash = HashSecret(Guid.NewGuid().ToString()),
            Scope = "[\"api.secrets\"]",
            EncryptedPayload = "2.payload|iv|mac",
            Key = "2.key|iv|mac",
        });

    private static string HashSecret(string secret) =>
        Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(secret)));

    /// <summary>
    /// Inserts a service account directly, since the Secrets Manager repositories aren't registered in this project.
    /// </summary>
    private static async Task<Guid> CreateServiceAccountAsync(
        IServiceProvider services, Database database, Guid organizationId)
    {
        var id = Guid.NewGuid();
        var now = DateTime.UtcNow;
        if (database.Type == SupportedDatabaseProviders.SqlServer && !database.UseEf)
        {
            await using var connection = new SqlConnection(database.ConnectionString);
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText =
                "INSERT INTO [dbo].[ServiceAccount] ([Id], [OrganizationId], [Name], [CreationDate], [RevisionDate]) " +
                "VALUES (@Id, @OrganizationId, @Name, @Now, @Now)";
            command.Parameters.AddWithValue("@Id", id);
            command.Parameters.AddWithValue("@OrganizationId", organizationId);
            command.Parameters.AddWithValue("@Name", "Test machine account");
            command.Parameters.AddWithValue("@Now", now);
            await command.ExecuteNonQueryAsync();
            return id;
        }

        using var scope = services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DatabaseContext>();
        dbContext.ServiceAccount.Add(new EntityFramework.SecretsManager.Models.ServiceAccount
        {
            Id = id,
            OrganizationId = organizationId,
            Name = "Test machine account",
            CreationDate = now,
            RevisionDate = now,
        });
        await dbContext.SaveChangesAsync();
        return id;
    }

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
            command.CommandText = "SELECT * FROM [dbo].[ApiKey] WHERE [Name] = @Name";
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
            $"SELECT * FROM {sql.DelimitIdentifier("ApiKey")} WHERE {sql.DelimitIdentifier("Name")} = @name";
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
