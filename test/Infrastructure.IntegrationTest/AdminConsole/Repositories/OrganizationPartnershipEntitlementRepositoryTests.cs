using Bit.Core;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums.Partnerships;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using Bit.Infrastructure.EntityFramework.Repositories;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Bit.Infrastructure.IntegrationTest.AdminConsole.Repositories;

public class OrganizationPartnershipEntitlementRepositoryTests
{
    private static readonly DateTime _asOf = new(2000, 6, 1, 0, 0, 0, DateTimeKind.Utc);

    [Theory, DatabaseData]
    public async Task CreateAsync_RoundTripsAllFields(
        IOrganizationPartnershipEntitlementRepository repository,
        IOrganizationPartnershipRepository partnershipRepository,
        IOrganizationRepository organizationRepository,
        IUserRepository userRepository)
    {
        var partnership = await CreatePartnershipAsync(partnershipRepository, organizationRepository);
        var user = await userRepository.CreateTestUserAsync();
        var date = new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc);
        var entitlement = NewEntitlement(partnership, "customer-1");
        entitlement.State = PartnershipEntitlementState.Canceled;
        entitlement.UserId = user.Id;
        entitlement.SetNewAccountRef();
        entitlement.SetMetadata(new Dictionary<string, string> { ["tier"] = "gold" });
        entitlement.BoundDate = date;
        entitlement.SuspendedDate = date.AddDays(1);
        entitlement.CanceledDate = date.AddDays(2);
        entitlement.ResumeWindowExpirationDate = date.AddDays(3);
        entitlement.LastAppliedEffectiveDate = date.AddDays(2);
        entitlement.CreationDate = date;
        entitlement.RevisionDate = date.AddDays(2);

        await repository.CreateAsync(entitlement);
        var result = await repository.GetByIdAsync(entitlement.Id);

        Assert.NotNull(result);
        Assert.Equal(partnership.Id, result.OrganizationPartnershipId);
        Assert.Equal("customer-1", result.ExternalId);
        Assert.Equal(entitlement.ExternalIdHash, result.ExternalIdHash);
        Assert.Equal(PartnershipEntitlementState.Canceled, result.State);
        Assert.Equal(user.Id, result.UserId);
        Assert.Equal(entitlement.AccountRef, result.AccountRef);
        Assert.Equal("gold", result.GetMetadata()["tier"]);
        Assert.Equal(date, result.BoundDate);
        Assert.Equal(date.AddDays(1), result.SuspendedDate);
        Assert.Equal(date.AddDays(2), result.CanceledDate);
        Assert.Equal(date.AddDays(3), result.ResumeWindowExpirationDate);
        Assert.Equal(date.AddDays(2), result.LastAppliedEffectiveDate);
        Assert.Equal(date, result.CreationDate);
        Assert.Equal(date.AddDays(2), result.RevisionDate);
    }

    [Theory, DatabaseData]
    public async Task CreateAsync_CallerEntityKeepsPlaintextExternalId(
        IOrganizationPartnershipEntitlementRepository repository,
        IOrganizationPartnershipRepository partnershipRepository,
        IOrganizationRepository organizationRepository)
    {
        var partnership = await CreatePartnershipAsync(partnershipRepository, organizationRepository);
        var entitlement = NewEntitlement(partnership, "customer-1");

        await repository.CreateAsync(entitlement);

        Assert.Equal("customer-1", entitlement.ExternalId);
    }

    [Theory, DatabaseData]
    public async Task CreateAsync_StoresExternalIdProtected(
        IOrganizationPartnershipEntitlementRepository repository,
        IOrganizationPartnershipRepository partnershipRepository,
        IOrganizationRepository organizationRepository,
        IDataProtectionProvider dataProtectionProvider,
        Database database,
        IServiceProvider services)
    {
        var partnership = await CreatePartnershipAsync(partnershipRepository, organizationRepository);
        var entitlement = await repository.CreateAsync(NewEntitlement(partnership, "customer-1"));

        var stored = await GetStoredExternalIdAsync(services, database, entitlement.Id);

        Assert.StartsWith(Constants.DatabaseFieldProtectedPrefix, stored);
        var dataProtector = dataProtectionProvider.CreateProtector(Constants.DatabaseFieldProtectorPurpose);
        Assert.Equal("customer-1",
            dataProtector.Unprotect(stored.Substring(Constants.DatabaseFieldProtectedPrefix.Length)));
    }

    [Theory, DatabaseData]
    public async Task CreateAsync_DuplicateExternalIdInPartnership_Throws(
        IOrganizationPartnershipEntitlementRepository repository,
        IOrganizationPartnershipRepository partnershipRepository,
        IOrganizationRepository organizationRepository)
    {
        var partnership = await CreatePartnershipAsync(partnershipRepository, organizationRepository);
        await repository.CreateAsync(NewEntitlement(partnership, "customer-1"));

        await Assert.ThrowsAnyAsync<Exception>(
            () => repository.CreateAsync(NewEntitlement(partnership, "customer-1")));
    }

    [Theory, DatabaseData]
    public async Task ReplaceAsync_UpdatesFields(
        IOrganizationPartnershipEntitlementRepository repository,
        IOrganizationPartnershipRepository partnershipRepository,
        IOrganizationRepository organizationRepository,
        IUserRepository userRepository)
    {
        var partnership = await CreatePartnershipAsync(partnershipRepository, organizationRepository);
        var user = await userRepository.CreateTestUserAsync();
        var entitlement = await repository.CreateAsync(NewEntitlement(partnership, "customer-1"));
        var boundDate = new DateTime(2026, 2, 3, 4, 5, 6, DateTimeKind.Utc);

        entitlement.State = PartnershipEntitlementState.Active;
        entitlement.UserId = user.Id;
        entitlement.SetNewAccountRef();
        entitlement.BoundDate = boundDate;
        entitlement.LastAppliedEffectiveDate = boundDate;
        await repository.ReplaceAsync(entitlement);

        var result = await repository.GetByIdAsync(entitlement.Id);

        Assert.NotNull(result);
        Assert.Equal("customer-1", result.ExternalId);
        Assert.Equal(PartnershipEntitlementState.Active, result.State);
        Assert.Equal(user.Id, result.UserId);
        Assert.Equal(entitlement.AccountRef, result.AccountRef);
        Assert.Equal(boundDate, result.BoundDate);
        Assert.Equal(boundDate, result.LastAppliedEffectiveDate);
    }

    [Theory, DatabaseData]
    public async Task ReplaceAsync_CallerEntityKeepsPlaintextExternalId(
        IOrganizationPartnershipEntitlementRepository repository,
        IOrganizationPartnershipRepository partnershipRepository,
        IOrganizationRepository organizationRepository)
    {
        var partnership = await CreatePartnershipAsync(partnershipRepository, organizationRepository);
        var entitlement = await repository.CreateAsync(NewEntitlement(partnership, "customer-1"));

        entitlement.State = PartnershipEntitlementState.Suspended;
        await repository.ReplaceAsync(entitlement);

        Assert.Equal("customer-1", entitlement.ExternalId);
    }

    [Theory, DatabaseData]
    public async Task ReplaceAsync_ReprovisionCanceledEntitlement_BindsToDifferentUser(
        IOrganizationPartnershipEntitlementRepository repository,
        IOrganizationPartnershipRepository partnershipRepository,
        IOrganizationRepository organizationRepository,
        IUserRepository userRepository)
    {
        var partnership = await CreatePartnershipAsync(partnershipRepository, organizationRepository);
        var firstUser = await userRepository.CreateTestUserAsync("first");
        var secondUser = await userRepository.CreateTestUserAsync("second");
        var canceled = NewEntitlement(partnership, "customer-1");
        canceled.State = PartnershipEntitlementState.Canceled;
        canceled.UserId = firstUser.Id;
        canceled.SetNewAccountRef();
        await repository.CreateAsync(canceled);
        var firstAccountRef = canceled.AccountRef;

        var reprovisioned = await repository.GetByExternalIdAsync(partnership.Id, "customer-1");
        Assert.NotNull(reprovisioned);
        reprovisioned.State = PartnershipEntitlementState.Active;
        reprovisioned.UserId = secondUser.Id;
        reprovisioned.SetNewAccountRef();
        await repository.ReplaceAsync(reprovisioned);

        var result = await repository.GetByExternalIdAsync(partnership.Id, "customer-1");

        Assert.NotNull(result);
        Assert.Equal(canceled.Id, result.Id);
        Assert.Equal(PartnershipEntitlementState.Active, result.State);
        Assert.Equal(secondUser.Id, result.UserId);
        Assert.NotEqual(firstAccountRef, result.AccountRef);
    }

    [Theory, DatabaseData]
    public async Task DeleteAsync_RemovesEntitlement(
        IOrganizationPartnershipEntitlementRepository repository,
        IOrganizationPartnershipRepository partnershipRepository,
        IOrganizationRepository organizationRepository)
    {
        var partnership = await CreatePartnershipAsync(partnershipRepository, organizationRepository);
        var entitlement = await repository.CreateAsync(NewEntitlement(partnership, "customer-1"));

        await repository.DeleteAsync(entitlement);

        Assert.Null(await repository.GetByIdAsync(entitlement.Id));
    }

    [Theory, DatabaseData]
    public async Task GetByExternalIdAsync_KnownExternalId_ReturnsEntitlement(
        IOrganizationPartnershipEntitlementRepository repository,
        IOrganizationPartnershipRepository partnershipRepository,
        IOrganizationRepository organizationRepository)
    {
        var partnership = await CreatePartnershipAsync(partnershipRepository, organizationRepository);
        var entitlement = await repository.CreateAsync(NewEntitlement(partnership, "customer-1"));

        var result = await repository.GetByExternalIdAsync(partnership.Id, "customer-1");

        Assert.NotNull(result);
        Assert.Equal(entitlement.Id, result.Id);
        Assert.Equal("customer-1", result.ExternalId);
    }

    [Theory, DatabaseData]
    public async Task GetByExternalIdAsync_UnknownExternalId_ReturnsNull(
        IOrganizationPartnershipEntitlementRepository repository,
        IOrganizationPartnershipRepository partnershipRepository,
        IOrganizationRepository organizationRepository)
    {
        var partnership = await CreatePartnershipAsync(partnershipRepository, organizationRepository);
        await repository.CreateAsync(NewEntitlement(partnership, "customer-1"));

        var result = await repository.GetByExternalIdAsync(partnership.Id, "customer-2");

        Assert.Null(result);
    }

    [Theory, DatabaseData]
    public async Task GetByExternalIdAsync_SameExternalIdUnderTwoPartnerships_ReturnsEachPartnershipsEntitlement(
        IOrganizationPartnershipEntitlementRepository repository,
        IOrganizationPartnershipRepository partnershipRepository,
        IOrganizationRepository organizationRepository)
    {
        var firstPartnership = await CreatePartnershipAsync(partnershipRepository, organizationRepository);
        var secondPartnership = await CreatePartnershipAsync(partnershipRepository, organizationRepository);
        var firstEntitlement = await repository.CreateAsync(NewEntitlement(firstPartnership, "customer-1"));
        var secondEntitlement = await repository.CreateAsync(NewEntitlement(secondPartnership, "customer-1"));

        var firstResult = await repository.GetByExternalIdAsync(firstPartnership.Id, "customer-1");
        var secondResult = await repository.GetByExternalIdAsync(secondPartnership.Id, "customer-1");

        Assert.NotNull(firstResult);
        Assert.NotNull(secondResult);
        Assert.Equal(firstEntitlement.Id, firstResult.Id);
        Assert.Equal(secondEntitlement.Id, secondResult.Id);
    }

    [Theory, DatabaseData]
    public async Task GetManyCanceledWithExpiredResumeWindowAsync_CanceledBoundAndExpired_IsReturned(
        IOrganizationPartnershipEntitlementRepository repository,
        IOrganizationPartnershipRepository partnershipRepository,
        IOrganizationRepository organizationRepository,
        IUserRepository userRepository)
    {
        var partnership = await CreatePartnershipAsync(partnershipRepository, organizationRepository);
        var user = await userRepository.CreateTestUserAsync();
        var beforeAsOf = await CreateCanceledAsync(repository, partnership, "before", user.Id, _asOf.AddDays(-1));
        var atAsOf = await CreateCanceledAsync(repository, partnership, "at", user.Id, _asOf);

        var results = await GetExpiredForPartnershipAsync(repository, partnership);

        Assert.Equal(new[] { atAsOf.Id, beforeAsOf.Id }.Order(), results.Select(e => e.Id).Order());
    }

    [Theory, DatabaseData]
    public async Task GetManyCanceledWithExpiredResumeWindowAsync_ReturnsPlaintextExternalId(
        IOrganizationPartnershipEntitlementRepository repository,
        IOrganizationPartnershipRepository partnershipRepository,
        IOrganizationRepository organizationRepository,
        IUserRepository userRepository)
    {
        var partnership = await CreatePartnershipAsync(partnershipRepository, organizationRepository);
        var user = await userRepository.CreateTestUserAsync();
        await CreateCanceledAsync(repository, partnership, "customer-1", user.Id, _asOf.AddDays(-1));

        var results = await GetExpiredForPartnershipAsync(repository, partnership);

        Assert.Equal("customer-1", Assert.Single(results).ExternalId);
    }

    [Theory, DatabaseData]
    public async Task GetManyCanceledWithExpiredResumeWindowAsync_ResumeWindowNotExpired_IsExcluded(
        IOrganizationPartnershipEntitlementRepository repository,
        IOrganizationPartnershipRepository partnershipRepository,
        IOrganizationRepository organizationRepository,
        IUserRepository userRepository)
    {
        var partnership = await CreatePartnershipAsync(partnershipRepository, organizationRepository);
        var user = await userRepository.CreateTestUserAsync();
        await CreateCanceledAsync(repository, partnership, "customer-1", user.Id, _asOf.AddSeconds(1));

        var results = await GetExpiredForPartnershipAsync(repository, partnership);

        Assert.Empty(results);
    }

    [Theory, DatabaseData]
    public async Task GetManyCanceledWithExpiredResumeWindowAsync_Unbound_IsExcluded(
        IOrganizationPartnershipEntitlementRepository repository,
        IOrganizationPartnershipRepository partnershipRepository,
        IOrganizationRepository organizationRepository)
    {
        var partnership = await CreatePartnershipAsync(partnershipRepository, organizationRepository);
        await CreateCanceledAsync(repository, partnership, "customer-1", null, _asOf.AddDays(-1));

        var results = await GetExpiredForPartnershipAsync(repository, partnership);

        Assert.Empty(results);
    }

    [Theory, DatabaseData]
    public async Task GetManyCanceledWithExpiredResumeWindowAsync_NotCanceled_IsExcluded(
        IOrganizationPartnershipEntitlementRepository repository,
        IOrganizationPartnershipRepository partnershipRepository,
        IOrganizationRepository organizationRepository,
        IUserRepository userRepository)
    {
        var partnership = await CreatePartnershipAsync(partnershipRepository, organizationRepository);
        var user = await userRepository.CreateTestUserAsync();
        var suspended = NewEntitlement(partnership, "customer-1");
        suspended.State = PartnershipEntitlementState.Suspended;
        suspended.UserId = user.Id;
        suspended.ResumeWindowExpirationDate = _asOf.AddDays(-1);
        await repository.CreateAsync(suspended);

        var results = await GetExpiredForPartnershipAsync(repository, partnership);

        Assert.Empty(results);
    }

    [Theory, DatabaseData]
    public async Task DeleteOrganization_DeletesEntitlements(
        IOrganizationPartnershipEntitlementRepository repository,
        IOrganizationPartnershipRepository partnershipRepository,
        IOrganizationRepository organizationRepository)
    {
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var partnership = await CreatePartnershipAsync(partnershipRepository, organization);
        var entitlement = await repository.CreateAsync(NewEntitlement(partnership, "customer-1"));

        await organizationRepository.DeleteAsync(organization);

        Assert.Null(await repository.GetByIdAsync(entitlement.Id));
    }

    [Theory, DatabaseData]
    public async Task DeleteUser_ClearsBindingOnEntitlement(
        IOrganizationPartnershipEntitlementRepository repository,
        IOrganizationPartnershipRepository partnershipRepository,
        IOrganizationRepository organizationRepository,
        IUserRepository userRepository)
    {
        var partnership = await CreatePartnershipAsync(partnershipRepository, organizationRepository);
        var user = await userRepository.CreateTestUserAsync();
        var entitlement = await CreateBoundAsync(repository, partnership, "customer-1", user);

        await userRepository.DeleteAsync(user);

        var result = await repository.GetByIdAsync(entitlement.Id);
        Assert.NotNull(result);
        Assert.Null(result.UserId);
        Assert.Null(result.AccountRef);
    }

    [Theory, DatabaseData]
    public async Task DeleteUser_LeavesStateAndRevisionDateUnchanged(
        IOrganizationPartnershipEntitlementRepository repository,
        IOrganizationPartnershipRepository partnershipRepository,
        IOrganizationRepository organizationRepository,
        IUserRepository userRepository)
    {
        var partnership = await CreatePartnershipAsync(partnershipRepository, organizationRepository);
        var user = await userRepository.CreateTestUserAsync();
        var entitlement = NewEntitlement(partnership, "customer-1");
        entitlement.State = PartnershipEntitlementState.Active;
        entitlement.UserId = user.Id;
        entitlement.SetNewAccountRef();
        entitlement.RevisionDate = new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc);
        await repository.CreateAsync(entitlement);

        await userRepository.DeleteAsync(user);

        var result = await repository.GetByIdAsync(entitlement.Id);
        Assert.NotNull(result);
        Assert.Equal(PartnershipEntitlementState.Active, result.State);
        Assert.Equal(entitlement.RevisionDate, result.RevisionDate);
    }

    [Theory, DatabaseData]
    public async Task DeleteManyUsers_ClearsBindingOnEntitlements(
        IOrganizationPartnershipEntitlementRepository repository,
        IOrganizationPartnershipRepository partnershipRepository,
        IOrganizationRepository organizationRepository,
        IUserRepository userRepository)
    {
        var partnership = await CreatePartnershipAsync(partnershipRepository, organizationRepository);
        var firstUser = await userRepository.CreateTestUserAsync("first");
        var secondUser = await userRepository.CreateTestUserAsync("second");
        var first = await CreateBoundAsync(repository, partnership, "customer-1", firstUser);
        var second = await CreateBoundAsync(repository, partnership, "customer-2", secondUser);

        await userRepository.DeleteManyAsync([firstUser, secondUser]);

        foreach (var id in new[] { first.Id, second.Id })
        {
            var result = await repository.GetByIdAsync(id);
            Assert.NotNull(result);
            Assert.Null(result.UserId);
            Assert.Null(result.AccountRef);
        }
    }

    [Theory, DatabaseData]
    public async Task DeleteUser_LeavesOtherUsersBindingIntact(
        IOrganizationPartnershipEntitlementRepository repository,
        IOrganizationPartnershipRepository partnershipRepository,
        IOrganizationRepository organizationRepository,
        IUserRepository userRepository)
    {
        var partnership = await CreatePartnershipAsync(partnershipRepository, organizationRepository);
        var deletedUser = await userRepository.CreateTestUserAsync("deleted");
        var keptUser = await userRepository.CreateTestUserAsync("kept");
        await CreateBoundAsync(repository, partnership, "customer-1", deletedUser);
        var kept = await CreateBoundAsync(repository, partnership, "customer-2", keptUser);

        await userRepository.DeleteAsync(deletedUser);

        var result = await repository.GetByIdAsync(kept.Id);
        Assert.NotNull(result);
        Assert.Equal(keptUser.Id, result.UserId);
        Assert.Equal(kept.AccountRef, result.AccountRef);
    }

    private static async Task<OrganizationPartnership> CreatePartnershipAsync(
        IOrganizationPartnershipRepository partnershipRepository,
        IOrganizationRepository organizationRepository)
        => await CreatePartnershipAsync(
            partnershipRepository, await organizationRepository.CreateTestOrganizationAsync());

    private static Task<OrganizationPartnership> CreatePartnershipAsync(
        IOrganizationPartnershipRepository partnershipRepository, Organization organization)
        => partnershipRepository.CreateAsync(new OrganizationPartnership
        {
            OrganizationId = organization.Id,
            Name = "Partner",
            Status = PartnershipStatus.Active,
            SponsoredPlanType = SponsoredPlanType.Premium,
            BindingMode = PartnershipBindingMode.Token,
        });

    private static OrganizationPartnershipEntitlement NewEntitlement(
        OrganizationPartnership partnership, string externalId)
        => new()
        {
            OrganizationPartnershipId = partnership.Id,
            ExternalId = externalId,
            ExternalIdHash = OrganizationPartnershipEntitlement.ComputeExternalIdHash(partnership.Id, externalId),
            State = PartnershipEntitlementState.Provisioned,
            LastAppliedEffectiveDate = DateTime.UtcNow,
        };

    private static Task<OrganizationPartnershipEntitlement> CreateCanceledAsync(
        IOrganizationPartnershipEntitlementRepository repository, OrganizationPartnership partnership,
        string externalId, Guid? userId, DateTime resumeWindowExpirationDate)
    {
        var entitlement = NewEntitlement(partnership, externalId);
        entitlement.State = PartnershipEntitlementState.Canceled;
        entitlement.UserId = userId;
        entitlement.ResumeWindowExpirationDate = resumeWindowExpirationDate;
        return repository.CreateAsync(entitlement);
    }

    private static Task<OrganizationPartnershipEntitlement> CreateBoundAsync(
        IOrganizationPartnershipEntitlementRepository repository, OrganizationPartnership partnership,
        string externalId, User user)
    {
        var entitlement = NewEntitlement(partnership, externalId);
        entitlement.State = PartnershipEntitlementState.Active;
        entitlement.UserId = user.Id;
        entitlement.SetNewAccountRef();
        return repository.CreateAsync(entitlement);
    }

    private static async Task<List<OrganizationPartnershipEntitlement>> GetExpiredForPartnershipAsync(
        IOrganizationPartnershipEntitlementRepository repository, OrganizationPartnership partnership)
        => (await repository.GetManyCanceledWithExpiredResumeWindowAsync(_asOf))
            .Where(e => e.OrganizationPartnershipId == partnership.Id)
            .ToList();

    private static async Task<string> GetStoredExternalIdAsync(
        IServiceProvider services, Database database, Guid id)
    {
        if (database.Type == SupportedDatabaseProviders.SqlServer && !database.UseEf)
        {
            await using var connection = new SqlConnection(database.ConnectionString);
            await connection.OpenAsync();
            await using var cmd = connection.CreateCommand();
            cmd.CommandText =
                "SELECT [ExternalId] FROM [dbo].[OrganizationPartnershipEntitlement] WHERE [Id] = @Id";
            cmd.Parameters.AddWithValue("@Id", id);
            return (string)(await cmd.ExecuteScalarAsync())!;
        }

        using var scope = services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DatabaseContext>();
        return await dbContext.OrganizationPartnershipEntitlements
            .AsNoTracking()
            .Where(e => e.Id == id)
            .Select(e => e.ExternalId)
            .SingleAsync();
    }
}
