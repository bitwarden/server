using AutoFixture;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.Dirt.Entities;
using Bit.Core.Dirt.Repositories;
using Bit.Core.Repositories;
using Bit.Core.Test.AutoFixture.Attributes;
using Bit.Infrastructure.Dapper.Dirt;
using Bit.Infrastructure.EFIntegration.Test.AutoFixture;
using Xunit;
using EfRepo = Bit.Infrastructure.EntityFramework.Repositories;
using SqlRepo = Bit.Infrastructure.Dapper.Repositories;

namespace Bit.Infrastructure.EFIntegration.Test.Dirt.Repositories;

public class OrganizationReportRepositoryTests
{
    [CiSkippedTheory, EfOrganizationReportAutoData]
    public async Task CreateAsync_ShouldCreateReport_WhenValidDataProvided(
        OrganizationReport report,
        Organization organization,
        List<EntityFramework.Dirt.Repositories.OrganizationReportRepository> suts,
        List<EfRepo.OrganizationRepository> efOrganizationRepos,
        OrganizationReportRepository sqlOrganizationReportRepo,
        SqlRepo.OrganizationRepository sqlOrganizationRepo)
    {
        var records = new List<OrganizationReport>();
        foreach (var sut in suts)
        {
            var i = suts.IndexOf(sut);

            var efOrganization = await efOrganizationRepos[i].CreateAsync(organization);
            sut.ClearChangeTracking();

            report.OrganizationId = efOrganization.Id;
            var postEfOrganizationReport = await sut.CreateAsync(report);
            sut.ClearChangeTracking();

            var savedOrganizationReport = await sut.GetByIdAsync(postEfOrganizationReport.Id);
            records.Add(savedOrganizationReport);
        }

        var sqlOrganization = await sqlOrganizationRepo.CreateAsync(organization);

        report.OrganizationId = sqlOrganization.Id;
        var sqlOrganizationReportRecord = await sqlOrganizationReportRepo.CreateAsync(report);
        var savedSqlOrganizationReport = await sqlOrganizationReportRepo.GetByIdAsync(sqlOrganizationReportRecord.Id);
        records.Add(savedSqlOrganizationReport);

        Assert.True(records.Count == 4);
    }

    [CiSkippedTheory, EfOrganizationReportAutoData]
    public async Task RetrieveByOrganisation_Works(
        OrganizationReportRepository sqlOrganizationReportRepo,
        SqlRepo.OrganizationRepository sqlOrganizationRepo)
    {
        var (firstOrg, firstReport) = await CreateOrganizationAndReportAsync(sqlOrganizationRepo, sqlOrganizationReportRepo);
        var (secondOrg, secondReport) = await CreateOrganizationAndReportAsync(sqlOrganizationRepo, sqlOrganizationReportRepo);

        var firstRetrievedReport = await sqlOrganizationReportRepo.GetByIdAsync(firstReport.Id);
        var secondRetrievedReport = await sqlOrganizationReportRepo.GetByIdAsync(secondReport.Id);

        Assert.NotNull(firstRetrievedReport);
        Assert.NotNull(secondRetrievedReport);
        Assert.Equal(firstOrg.Id, firstRetrievedReport.OrganizationId);
        Assert.Equal(secondOrg.Id, secondRetrievedReport.OrganizationId);
    }

    [CiSkippedTheory, EfOrganizationReportAutoData]
    public async Task Delete_Works(
        List<EntityFramework.Dirt.Repositories.OrganizationReportRepository> suts,
        List<EfRepo.OrganizationRepository> efOrganizationRepos,
        OrganizationReportRepository sqlOrganizationReportRepo,
        SqlRepo.OrganizationRepository sqlOrganizationRepo)
    {
        var fixture = new Fixture();
        var rawOrg = fixture.Build<Organization>().Create();
        var rawRecord = fixture.Build<OrganizationReport>()
                            .With(_ => _.OrganizationId, rawOrg.Id)
                            .Create();
        var dbRecords = new List<OrganizationReport>();

        foreach (var sut in suts)
        {
            var i = suts.IndexOf(sut);

            // create a new organization for each repository
            var organization = await efOrganizationRepos[i].CreateAsync(rawOrg);

            // map the organization Id and use Upsert to save new record
            rawRecord.OrganizationId = organization.Id;
            rawRecord = await sut.CreateAsync(rawRecord);
            sut.ClearChangeTracking();

            // apply update using Upsert to make changes to db
            await sut.DeleteAsync(rawRecord);
            sut.ClearChangeTracking();

            // retrieve the data and add to the list for assertions
            var recordFromDb = await sut.GetByIdAsync(rawRecord.Id);
            dbRecords.Add(recordFromDb);

            sut.ClearChangeTracking();
        }

        // sql - create new records
        var (org, organizationReport) = await CreateOrganizationAndReportAsync(sqlOrganizationRepo, sqlOrganizationReportRepo);
        await sqlOrganizationReportRepo.DeleteAsync(organizationReport);
        var sqlDbRecord = await sqlOrganizationReportRepo.GetByIdAsync(organizationReport.Id);
        dbRecords.Add(sqlDbRecord);

        // assertions
        // all records should be null - as they were deleted before querying
        Assert.True(dbRecords.Where(_ => _ == null).Count() == 4);
    }

    [CiSkippedTheory, EfOrganizationReportAutoData]
    public async Task GetLatestByOrganizationIdAsync_ShouldReturnLatestReport(
        OrganizationReportRepository sqlOrganizationReportRepo,
        SqlRepo.OrganizationRepository sqlOrganizationRepo)
    {
        // Arrange
        var (org, firstReport) = await CreateOrganizationAndReportAsync(sqlOrganizationRepo, sqlOrganizationReportRepo);

        // Create a second report for the same organization with a later revision date
        var fixture = new Fixture();
        var secondReport = fixture.Build<OrganizationReport>()
            .With(x => x.OrganizationId, org.Id)
            .With(x => x.RevisionDate, DateTime.UtcNow.AddMinutes(1))
            .Create();
        await sqlOrganizationReportRepo.CreateAsync(secondReport);

        // Act
        var latestReport = await sqlOrganizationReportRepo.GetLatestByOrganizationIdAsync(org.Id);

        // Assert
        Assert.NotNull(latestReport);
        Assert.Equal(org.Id, latestReport.OrganizationId);
        Assert.True(latestReport.RevisionDate >= firstReport.RevisionDate);
    }

    [CiSkippedTheory, EfOrganizationReportAutoData]
    public async Task UpdateSummaryDataAsync_ShouldUpdateSummaryAndRevisionDate(
        OrganizationReportRepository sqlOrganizationReportRepo,
        SqlRepo.OrganizationRepository sqlOrganizationRepo)
    {
        // Arrange
        var (_, report) = await CreateOrganizationAndReportAsync(sqlOrganizationRepo, sqlOrganizationReportRepo);
        report.RevisionDate = DateTime.UtcNow.Subtract(TimeSpan.FromDays(1)); // ensure old revision date
        var newSummaryData = "Updated summary data";
        var originalRevisionDate = report.RevisionDate;

        // Act
        var updatedReport = await sqlOrganizationReportRepo.UpdateSummaryDataAsync(report.OrganizationId, report.Id, newSummaryData);

        // Assert
        Assert.NotNull(updatedReport);
        Assert.Equal(newSummaryData, updatedReport.SummaryData);
        Assert.True(updatedReport.RevisionDate > originalRevisionDate);
    }

    [CiSkippedTheory, EfOrganizationReportAutoData]
    public async Task GetSummaryDataAsync_ShouldReturnSummaryData(
        OrganizationReportRepository sqlOrganizationReportRepo,
        SqlRepo.OrganizationRepository sqlOrganizationRepo)
    {
        // Arrange
        var summaryData = "Test summary data";
        var (org, report) = await CreateOrganizationAndReportWithSummaryDataAsync(
            sqlOrganizationRepo, sqlOrganizationReportRepo, summaryData);

        // Act
        var result = await sqlOrganizationReportRepo.GetSummaryDataAsync(org.Id, report.Id);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(summaryData, result.SummaryData);
    }

    [CiSkippedTheory, EfOrganizationReportAutoData]
    public async Task GetSummaryDataByDateRangeAsync_ShouldReturnFilteredResults(
        OrganizationReportRepository sqlOrganizationReportRepo,
        SqlRepo.OrganizationRepository sqlOrganizationRepo)
    {
        // Arrange
        var baseDate = DateTime.UtcNow;
        var startDate = baseDate.AddDays(-10);
        var endDate = baseDate.AddDays(1);

        // Create organization first
        var fixture = new Fixture();
        var organization = fixture.Create<Organization>();
        var org = await sqlOrganizationRepo.CreateAsync(organization);

        // Create first report with a date within range
        var report1 = fixture.Build<OrganizationReport>()
            .With(x => x.OrganizationId, org.Id)
            .With(x => x.SummaryData, "Summary 1")
            .With(x => x.CreationDate, baseDate.AddDays(-5)) // Within range
            .With(x => x.RevisionDate, baseDate.AddDays(-5))
            .Create();
        await sqlOrganizationReportRepo.CreateAsync(report1);

        // Create second report with a date within range
        var report2 = fixture.Build<OrganizationReport>()
            .With(x => x.OrganizationId, org.Id)
            .With(x => x.SummaryData, "Summary 2")
            .With(x => x.CreationDate, baseDate.AddDays(-3)) // Within range
            .With(x => x.RevisionDate, baseDate.AddDays(-3))
            .Create();
        await sqlOrganizationReportRepo.CreateAsync(report2);

        // Act
        var results = await sqlOrganizationReportRepo.GetSummaryDataByDateRangeAsync(
            org.Id, startDate, endDate);

        // Assert
        Assert.NotNull(results);
        var resultsList = results.ToList();
        Assert.True(resultsList.Count >= 2, $"Expected at least 2 results, but got {resultsList.Count}");
        Assert.All(resultsList, r => Assert.NotNull(r.SummaryData));
    }

    [CiSkippedTheory, EfOrganizationReportAutoData]
    public async Task GetReportDataAsync_ShouldReturnReportData(
        OrganizationReportRepository sqlOrganizationReportRepo,
        SqlRepo.OrganizationRepository sqlOrganizationRepo)
    {
        // Arrange
        var fixture = new Fixture();
        var reportData = "Test report data";
        var (org, report) = await CreateOrganizationAndReportWithReportDataAsync(
            sqlOrganizationRepo, sqlOrganizationReportRepo, reportData);

        // Act
        var result = await sqlOrganizationReportRepo.GetReportDataAsync(org.Id, report.Id);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(reportData, result.ReportData);
    }

    [CiSkippedTheory, EfOrganizationReportAutoData]
    public async Task UpdateReportDataAsync_ShouldUpdateReportDataAndRevisionDate(
        OrganizationReportRepository sqlOrganizationReportRepo,
        SqlRepo.OrganizationRepository sqlOrganizationRepo)
    {
        // Arrange
        var (org, report) = await CreateOrganizationAndReportAsync(sqlOrganizationRepo, sqlOrganizationReportRepo);
        var newReportData = "Updated report data";
        var originalRevisionDate = report.RevisionDate;

        // Add a small delay to ensure revision date difference
        await Task.Delay(100);

        // Act
        var updatedReport = await sqlOrganizationReportRepo.UpdateReportDataAsync(
            org.Id, report.Id, newReportData);

        // Assert
        Assert.NotNull(updatedReport);
        Assert.Equal(org.Id, updatedReport.OrganizationId);
        Assert.Equal(report.Id, updatedReport.Id);
        Assert.Equal(newReportData, updatedReport.ReportData);
        Assert.True(updatedReport.RevisionDate >= originalRevisionDate,
            $"Expected RevisionDate {updatedReport.RevisionDate} to be >= {originalRevisionDate}");
    }

    [CiSkippedTheory, EfOrganizationReportAutoData]
    public async Task GetApplicationDataAsync_ShouldReturnApplicationData(
        OrganizationReportRepository sqlOrganizationReportRepo,
        SqlRepo.OrganizationRepository sqlOrganizationRepo)
    {
        // Arrange
        var applicationData = "Test application data";
        var (org, report) = await CreateOrganizationAndReportWithApplicationDataAsync(
            sqlOrganizationRepo, sqlOrganizationReportRepo, applicationData);

        // Act
        var result = await sqlOrganizationReportRepo.GetApplicationDataAsync(org.Id, report.Id);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(applicationData, result.ApplicationData);
    }

    [CiSkippedTheory, EfOrganizationReportAutoData]
    public async Task UpdateApplicationDataAsync_ShouldUpdateApplicationDataAndRevisionDate(
        OrganizationReportRepository sqlOrganizationReportRepo,
        SqlRepo.OrganizationRepository sqlOrganizationRepo)
    {
        // Arrange
        var (org, report) = await CreateOrganizationAndReportAsync(sqlOrganizationRepo, sqlOrganizationReportRepo);
        var newApplicationData = "Updated application data";
        var originalRevisionDate = report.RevisionDate;

        // Add a small delay to ensure revision date difference
        await Task.Delay(100);

        // Act
        var updatedReport = await sqlOrganizationReportRepo.UpdateApplicationDataAsync(
            org.Id, report.Id, newApplicationData);

        // Assert
        Assert.NotNull(updatedReport);
        Assert.Equal(org.Id, updatedReport.OrganizationId);
        Assert.Equal(report.Id, updatedReport.Id);
        Assert.Equal(newApplicationData, updatedReport.ApplicationData);
        Assert.True(updatedReport.RevisionDate >= originalRevisionDate,
            $"Expected RevisionDate {updatedReport.RevisionDate} to be >= {originalRevisionDate}");
    }

    [CiSkippedTheory, EfOrganizationReportAutoData]
    public async Task GetSummaryDataAsync_WithNonExistentReport_ShouldReturnNull(
        OrganizationReportRepository sqlOrganizationReportRepo,
        SqlRepo.OrganizationRepository sqlOrganizationRepo)
    {
        // Arrange
        var (org, _) = await CreateOrganizationAndReportAsync(sqlOrganizationRepo, sqlOrganizationReportRepo);
        var nonExistentReportId = Guid.NewGuid();

        // Act
        var result = await sqlOrganizationReportRepo.GetSummaryDataAsync(org.Id, nonExistentReportId);

        // Assert
        Assert.Null(result);
    }

    [CiSkippedTheory, EfOrganizationReportAutoData]
    public async Task GetReportDataAsync_WithNonExistentReport_ShouldReturnNull(
        OrganizationReportRepository sqlOrganizationReportRepo,
        SqlRepo.OrganizationRepository sqlOrganizationRepo)
    {
        // Arrange
        var (org, _) = await CreateOrganizationAndReportAsync(sqlOrganizationRepo, sqlOrganizationReportRepo);
        var nonExistentReportId = Guid.NewGuid();

        // Act
        var result = await sqlOrganizationReportRepo.GetReportDataAsync(org.Id, nonExistentReportId);

        // Assert
        Assert.Null(result);
    }

    [CiSkippedTheory, EfOrganizationReportAutoData]
    public async Task GetApplicationDataAsync_WithNonExistentReport_ShouldReturnNull(
        OrganizationReportRepository sqlOrganizationReportRepo,
        SqlRepo.OrganizationRepository sqlOrganizationRepo)
    {
        // Arrange
        var (org, _) = await CreateOrganizationAndReportAsync(sqlOrganizationRepo, sqlOrganizationReportRepo);
        var nonExistentReportId = Guid.NewGuid();

        // Act
        var result = await sqlOrganizationReportRepo.GetApplicationDataAsync(org.Id, nonExistentReportId);

        // Assert
        Assert.Null(result);
    }

    private async Task<(Organization, OrganizationReport)> CreateOrganizationAndReportAsync(
        IOrganizationRepository orgRepo,
        IOrganizationReportRepository orgReportRepo)
    {
        var fixture = new Fixture();
        var organization = fixture.Create<Organization>();

        var orgReportRecord = fixture.Build<OrganizationReport>()
            .With(x => x.OrganizationId, organization.Id)
            .With(x => x.RevisionDate, organization.RevisionDate)
            .Create();

        organization = await orgRepo.CreateAsync(organization);
        orgReportRecord = await orgReportRepo.CreateAsync(orgReportRecord);

        return (organization, orgReportRecord);
    }

    private async Task<(Organization, OrganizationReport)> CreateOrganizationAndReportWithSummaryDataAsync(
        IOrganizationRepository orgRepo,
        IOrganizationReportRepository orgReportRepo,
        string summaryData)
    {
        var fixture = new Fixture();
        var organization = fixture.Create<Organization>();

        var orgReportRecord = fixture.Build<OrganizationReport>()
            .With(x => x.OrganizationId, organization.Id)
            .With(x => x.SummaryData, summaryData)
            .Create();

        organization = await orgRepo.CreateAsync(organization);
        orgReportRecord = await orgReportRepo.CreateAsync(orgReportRecord);

        return (organization, orgReportRecord);
    }

    private async Task<(Organization, OrganizationReport)> CreateOrganizationAndReportWithReportDataAsync(
        IOrganizationRepository orgRepo,
        IOrganizationReportRepository orgReportRepo,
        string reportData)
    {
        var fixture = new Fixture();
        var organization = fixture.Create<Organization>();

        var orgReportRecord = fixture.Build<OrganizationReport>()
            .With(x => x.OrganizationId, organization.Id)
            .With(x => x.ReportData, reportData)
            .Create();

        organization = await orgRepo.CreateAsync(organization);
        orgReportRecord = await orgReportRepo.CreateAsync(orgReportRecord);

        return (organization, orgReportRecord);
    }

    private async Task<(Organization, OrganizationReport)> CreateOrganizationAndReportWithApplicationDataAsync(
        IOrganizationRepository orgRepo,
        IOrganizationReportRepository orgReportRepo,
        string applicationData)
    {
        var fixture = new Fixture();
        var organization = fixture.Create<Organization>();

        var orgReportRecord = fixture.Build<OrganizationReport>()
            .With(x => x.OrganizationId, organization.Id)
            .With(x => x.ApplicationData, applicationData)
            .Create();

        organization = await orgRepo.CreateAsync(organization);
        orgReportRecord = await orgReportRepo.CreateAsync(orgReportRecord);

        return (organization, orgReportRecord);
    }
}
