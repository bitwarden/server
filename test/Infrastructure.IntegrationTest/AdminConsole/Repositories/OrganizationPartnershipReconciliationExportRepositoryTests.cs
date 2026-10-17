using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums.Partnerships;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Repositories;
using Xunit;

namespace Bit.Infrastructure.IntegrationTest.AdminConsole.Repositories;

public class OrganizationPartnershipReconciliationExportRepositoryTests
{
    private static readonly DateTime _asOf = new(2000, 6, 1, 0, 0, 0, DateTimeKind.Utc);

    [Theory, DatabaseData]
    public async Task CreateAsync_RoundTripsAllFields(
        IOrganizationPartnershipReconciliationExportRepository repository,
        IOrganizationPartnershipRepository partnershipRepository,
        IOrganizationRepository organizationRepository)
    {
        var partnership = await CreatePartnershipAsync(partnershipRepository, organizationRepository);
        var date = new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc);
        var export = new OrganizationPartnershipReconciliationExport
        {
            OrganizationPartnershipId = partnership.Id,
            Status = PartnershipReconciliationExportStatus.Completed,
            RecordCount = 42,
            BlobName = $"{partnership.Id}/export.ndjson",
            CreationDate = date,
            CompletionDate = date.AddMinutes(1),
            ExpirationDate = date.AddDays(1),
            RevisionDate = date.AddMinutes(1),
        };

        await repository.CreateAsync(export);
        var result = await repository.GetByIdAsync(export.Id);

        Assert.NotNull(result);
        Assert.Equal(partnership.Id, result.OrganizationPartnershipId);
        Assert.Equal(PartnershipReconciliationExportStatus.Completed, result.Status);
        Assert.Equal(42, result.RecordCount);
        Assert.Equal($"{partnership.Id}/export.ndjson", result.BlobName);
        Assert.Equal(date, result.CreationDate);
        Assert.Equal(date.AddMinutes(1), result.CompletionDate);
        Assert.Equal(date.AddDays(1), result.ExpirationDate);
        Assert.Equal(date.AddMinutes(1), result.RevisionDate);
    }

    [Theory, DatabaseData]
    public async Task ReplaceAsync_UpdatesFields(
        IOrganizationPartnershipReconciliationExportRepository repository,
        IOrganizationPartnershipRepository partnershipRepository,
        IOrganizationRepository organizationRepository)
    {
        var partnership = await CreatePartnershipAsync(partnershipRepository, organizationRepository);
        var export = await CreateExportAsync(repository, partnership, PartnershipReconciliationExportStatus.Running,
            _asOf.AddDays(1));

        export.Status = PartnershipReconciliationExportStatus.Failed;
        export.CompletionDate = _asOf;
        await repository.ReplaceAsync(export);
        var result = await repository.GetByIdAsync(export.Id);

        Assert.NotNull(result);
        Assert.Equal(PartnershipReconciliationExportStatus.Failed, result.Status);
        Assert.Equal(_asOf, result.CompletionDate);
    }

    [Theory, DatabaseData]
    public async Task GetManyPendingAsync_ReturnsOnlyPending(
        IOrganizationPartnershipReconciliationExportRepository repository,
        IOrganizationPartnershipRepository partnershipRepository,
        IOrganizationRepository organizationRepository)
    {
        var partnership = await CreatePartnershipAsync(partnershipRepository, organizationRepository);
        var pending = await CreateExportAsync(repository, partnership, PartnershipReconciliationExportStatus.Pending,
            _asOf.AddDays(1));
        await CreateExportAsync(repository, partnership, PartnershipReconciliationExportStatus.Running, _asOf.AddDays(1));
        await CreateExportAsync(repository, partnership, PartnershipReconciliationExportStatus.Completed, _asOf.AddDays(1));
        await CreateExportAsync(repository, partnership, PartnershipReconciliationExportStatus.Failed, _asOf.AddDays(1));

        var results = (await repository.GetManyPendingAsync())
            .Where(e => e.OrganizationPartnershipId == partnership.Id);

        Assert.Equal([pending.Id], results.Select(e => e.Id));
    }

    [Theory, DatabaseData]
    public async Task GetManyExpiredAsync_ReturnsExportsExpiredAtOrBeforeAsOf(
        IOrganizationPartnershipReconciliationExportRepository repository,
        IOrganizationPartnershipRepository partnershipRepository,
        IOrganizationRepository organizationRepository)
    {
        var partnership = await CreatePartnershipAsync(partnershipRepository, organizationRepository);
        var expiredEarlier = await CreateExportAsync(repository, partnership,
            PartnershipReconciliationExportStatus.Completed, _asOf.AddDays(-1));
        var expiredNow = await CreateExportAsync(repository, partnership, PartnershipReconciliationExportStatus.Pending,
            _asOf);
        await CreateExportAsync(repository, partnership, PartnershipReconciliationExportStatus.Completed,
            _asOf.AddDays(1));

        var results = (await repository.GetManyExpiredAsync(_asOf))
            .Where(e => e.OrganizationPartnershipId == partnership.Id)
            .Select(e => e.Id)
            .ToHashSet();

        Assert.Equal(new HashSet<Guid> { expiredEarlier.Id, expiredNow.Id }, results);
    }

    [Theory, DatabaseData]
    public async Task StartIfPendingAsync_Pending_MovesToRunning(
        IOrganizationPartnershipReconciliationExportRepository repository,
        IOrganizationPartnershipRepository partnershipRepository,
        IOrganizationRepository organizationRepository)
    {
        var partnership = await CreatePartnershipAsync(partnershipRepository, organizationRepository);
        var export = await CreateExportAsync(repository, partnership, PartnershipReconciliationExportStatus.Pending,
            _asOf.AddDays(1));

        var started = await repository.StartIfPendingAsync(export.Id, _asOf);
        var result = await repository.GetByIdAsync(export.Id);

        Assert.True(started);
        Assert.NotNull(result);
        Assert.Equal(PartnershipReconciliationExportStatus.Running, result.Status);
        Assert.Equal(_asOf, result.RevisionDate);
    }

    [Theory, DatabaseData]
    public async Task StartIfPendingAsync_AlreadyStarted_ReturnsFalse(
        IOrganizationPartnershipReconciliationExportRepository repository,
        IOrganizationPartnershipRepository partnershipRepository,
        IOrganizationRepository organizationRepository)
    {
        var partnership = await CreatePartnershipAsync(partnershipRepository, organizationRepository);
        var export = await CreateExportAsync(repository, partnership, PartnershipReconciliationExportStatus.Pending,
            _asOf.AddDays(1));
        await repository.StartIfPendingAsync(export.Id, _asOf);

        var startedAgain = await repository.StartIfPendingAsync(export.Id, _asOf.AddMinutes(1));

        Assert.False(startedAgain);
    }

    [Theory, DatabaseData]
    public async Task DeleteAsync_RemovesExport(
        IOrganizationPartnershipReconciliationExportRepository repository,
        IOrganizationPartnershipRepository partnershipRepository,
        IOrganizationRepository organizationRepository)
    {
        var partnership = await CreatePartnershipAsync(partnershipRepository, organizationRepository);
        var export = await CreateExportAsync(repository, partnership, PartnershipReconciliationExportStatus.Completed,
            _asOf);

        await repository.DeleteAsync(export);

        Assert.Null(await repository.GetByIdAsync(export.Id));
    }

    [Theory, DatabaseData]
    public async Task DeletePartnership_DeletesExports(
        IOrganizationPartnershipReconciliationExportRepository repository,
        IOrganizationPartnershipRepository partnershipRepository,
        IOrganizationRepository organizationRepository)
    {
        var partnership = await CreatePartnershipAsync(partnershipRepository, organizationRepository);
        var export = await CreateExportAsync(repository, partnership, PartnershipReconciliationExportStatus.Completed,
            _asOf.AddDays(1));

        await partnershipRepository.DeleteAsync(partnership);

        Assert.Null(await repository.GetByIdAsync(export.Id));
    }

    [Theory, DatabaseData]
    public async Task DeleteOrganization_DeletesExports(
        IOrganizationPartnershipReconciliationExportRepository repository,
        IOrganizationPartnershipRepository partnershipRepository,
        IOrganizationRepository organizationRepository)
    {
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var partnership = await partnershipRepository.CreateAsync(NewPartnership(organization));
        var export = await CreateExportAsync(repository, partnership, PartnershipReconciliationExportStatus.Completed,
            _asOf.AddDays(1));

        await organizationRepository.DeleteAsync(organization);

        Assert.Null(await repository.GetByIdAsync(export.Id));
    }

    private static async Task<OrganizationPartnership> CreatePartnershipAsync(
        IOrganizationPartnershipRepository partnershipRepository, IOrganizationRepository organizationRepository)
    {
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        return await partnershipRepository.CreateAsync(NewPartnership(organization));
    }

    private static OrganizationPartnership NewPartnership(Organization organization) =>
        new()
        {
            OrganizationId = organization.Id,
            Name = "Partner",
            Status = PartnershipStatus.Active,
            BindingMode = PartnershipBindingMode.Token,
        };

    private static Task<OrganizationPartnershipReconciliationExport> CreateExportAsync(
        IOrganizationPartnershipReconciliationExportRepository repository, OrganizationPartnership partnership,
        PartnershipReconciliationExportStatus status, DateTime expirationDate)
        => repository.CreateAsync(new OrganizationPartnershipReconciliationExport
        {
            OrganizationPartnershipId = partnership.Id,
            Status = status,
            CreationDate = _asOf.AddHours(-1),
            ExpirationDate = expirationDate,
            RevisionDate = _asOf.AddHours(-1),
        });
}
