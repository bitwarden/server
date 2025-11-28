using AutoFixture;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.Dirt.Entities;
using Bit.Core.Dirt.Reports.Models.Data;
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
    public async Task CreateAsync_ShouldPersistAllMetricProperties_WhenSet(
        List<EntityFramework.Dirt.Repositories.OrganizationReportRepository> suts,
        List<EfRepo.OrganizationRepository> efOrganizationRepos,
        OrganizationReportRepository sqlOrganizationReportRepo,
        SqlRepo.OrganizationRepository sqlOrganizationRepo)
    {
        // Arrange - Create a report with explicit metric values
        var fixture = new Fixture();
        var organization = fixture.Create<Organization>();
        var report = fixture.Build<OrganizationReport>()
            .With(x => x.ApplicationCount, 10)
            .With(x => x.ApplicationAtRiskCount, 3)
            .With(x => x.CriticalApplicationCount, 5)
            .With(x => x.CriticalApplicationAtRiskCount, 2)
            .With(x => x.MemberCount, 25)
            .With(x => x.MemberAtRiskCount, 7)
            .With(x => x.CriticalMemberCount, 12)
            .With(x => x.CriticalMemberAtRiskCount, 4)
            .With(x => x.PasswordCount, 100)
            .With(x => x.PasswordAtRiskCount, 15)
            .With(x => x.CriticalPasswordCount, 50)
            .With(x => x.CriticalPasswordAtRiskCount, 8)
            .Create();

        var retrievedReports = new List<OrganizationReport>();

        // Act & Assert - Test EF repositories
        foreach (var sut in suts)
        {
            var i = suts.IndexOf(sut);
            var efOrganization = await efOrganizationRepos[i].CreateAsync(organization);
            sut.ClearChangeTracking();

            report.OrganizationId = efOrganization.Id;
            var createdReport = await sut.CreateAsync(report);
            sut.ClearChangeTracking();

            var savedReport = await sut.GetByIdAsync(createdReport.Id);
            retrievedReports.Add(savedReport);
        }

        // Act & Assert - Test SQL repository
        var sqlOrganization = await sqlOrganizationRepo.CreateAsync(organization);
        report.OrganizationId = sqlOrganization.Id;
        var sqlCreatedReport = await sqlOrganizationReportRepo.CreateAsync(report);
        var savedSqlReport = await sqlOrganizationReportRepo.GetByIdAsync(sqlCreatedReport.Id);
        retrievedReports.Add(savedSqlReport);

        // Assert - Verify all metric properties are persisted correctly across all repositories
        Assert.True(retrievedReports.Count == 4);
        foreach (var retrievedReport in retrievedReports)
        {
            Assert.NotNull(retrievedReport);
            Assert.Equal(10, retrievedReport.ApplicationCount);
            Assert.Equal(3, retrievedReport.ApplicationAtRiskCount);
            Assert.Equal(5, retrievedReport.CriticalApplicationCount);
            Assert.Equal(2, retrievedReport.CriticalApplicationAtRiskCount);
            Assert.Equal(25, retrievedReport.MemberCount);
            Assert.Equal(7, retrievedReport.MemberAtRiskCount);
            Assert.Equal(12, retrievedReport.CriticalMemberCount);
            Assert.Equal(4, retrievedReport.CriticalMemberAtRiskCount);
            Assert.Equal(100, retrievedReport.PasswordCount);
            Assert.Equal(15, retrievedReport.PasswordAtRiskCount);
            Assert.Equal(50, retrievedReport.CriticalPasswordCount);
            Assert.Equal(8, retrievedReport.CriticalPasswordAtRiskCount);
        }
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
    public async Task UpdateAsync_ShouldUpdateAllMetricProperties_WhenChanged(
        OrganizationReportRepository sqlOrganizationReportRepo,
        SqlRepo.OrganizationRepository sqlOrganizationRepo)
    {
        // Arrange - Create initial report with specific metric values
        var fixture = new Fixture();
        var organization = fixture.Create<Organization>();
        var org = await sqlOrganizationRepo.CreateAsync(organization);

        var report = fixture.Build<OrganizationReport>()
            .With(x => x.OrganizationId, org.Id)
            .With(x => x.ApplicationCount, 10)
            .With(x => x.ApplicationAtRiskCount, 3)
            .With(x => x.CriticalApplicationCount, 5)
            .With(x => x.CriticalApplicationAtRiskCount, 2)
            .With(x => x.MemberCount, 25)
            .With(x => x.MemberAtRiskCount, 7)
            .With(x => x.CriticalMemberCount, 12)
            .With(x => x.CriticalMemberAtRiskCount, 4)
            .With(x => x.PasswordCount, 100)
            .With(x => x.PasswordAtRiskCount, 15)
            .With(x => x.CriticalPasswordCount, 50)
            .With(x => x.CriticalPasswordAtRiskCount, 8)
            .Create();

        var createdReport = await sqlOrganizationReportRepo.CreateAsync(report);

        // Act - Update all metric properties with new values
        createdReport.ApplicationCount = 20;
        createdReport.ApplicationAtRiskCount = 6;
        createdReport.CriticalApplicationCount = 10;
        createdReport.CriticalApplicationAtRiskCount = 4;
        createdReport.MemberCount = 50;
        createdReport.MemberAtRiskCount = 14;
        createdReport.CriticalMemberCount = 24;
        createdReport.CriticalMemberAtRiskCount = 8;
        createdReport.PasswordCount = 200;
        createdReport.PasswordAtRiskCount = 30;
        createdReport.CriticalPasswordCount = 100;
        createdReport.CriticalPasswordAtRiskCount = 16;

        await sqlOrganizationReportRepo.UpsertAsync(createdReport);

        // Assert - Verify all metric properties were updated correctly
        var updatedReport = await sqlOrganizationReportRepo.GetByIdAsync(createdReport.Id);
        Assert.NotNull(updatedReport);
        Assert.Equal(20, updatedReport.ApplicationCount);
        Assert.Equal(6, updatedReport.ApplicationAtRiskCount);
        Assert.Equal(10, updatedReport.CriticalApplicationCount);
        Assert.Equal(4, updatedReport.CriticalApplicationAtRiskCount);
        Assert.Equal(50, updatedReport.MemberCount);
        Assert.Equal(14, updatedReport.MemberAtRiskCount);
        Assert.Equal(24, updatedReport.CriticalMemberCount);
        Assert.Equal(8, updatedReport.CriticalMemberAtRiskCount);
        Assert.Equal(200, updatedReport.PasswordCount);
        Assert.Equal(30, updatedReport.PasswordAtRiskCount);
        Assert.Equal(100, updatedReport.CriticalPasswordCount);
        Assert.Equal(16, updatedReport.CriticalPasswordAtRiskCount);
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
            .With(x => x.RevisionDate, firstReport.RevisionDate.AddMinutes(30))
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
        var result = await sqlOrganizationReportRepo.GetSummaryDataAsync(report.Id);

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
        var result = await sqlOrganizationReportRepo.GetReportDataAsync(report.Id);

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
        var result = await sqlOrganizationReportRepo.GetApplicationDataAsync(report.Id);

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
        var originalRevisionDate = DateTime.UtcNow.Subtract(TimeSpan.FromDays(1)); // ensure old revision date

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
        var result = await sqlOrganizationReportRepo.GetSummaryDataAsync(nonExistentReportId);

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
        var result = await sqlOrganizationReportRepo.GetReportDataAsync(nonExistentReportId);

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
        var result = await sqlOrganizationReportRepo.GetApplicationDataAsync(nonExistentReportId);

        // Assert
        Assert.Null(result);
    }

    [CiSkippedTheory, EfOrganizationReportAutoData]
    public async Task UpdateMetricsAsync_ShouldUpdateMetricsCorrectly(
        OrganizationReportRepository sqlOrganizationReportRepo,
        SqlRepo.OrganizationRepository sqlOrganizationRepo)
    {
        // Arrange
        var (org, report) = await CreateOrganizationAndReportAsync(sqlOrganizationRepo, sqlOrganizationReportRepo);
        var metrics = new OrganizationReportMetricsData
        {
            ApplicationCount = 10,
            ApplicationAtRiskCount = 2,
            CriticalApplicationCount = 5,
            CriticalApplicationAtRiskCount = 1,
            MemberCount = 20,
            MemberAtRiskCount = 4,
            CriticalMemberCount = 10,
            CriticalMemberAtRiskCount = 2,
            PasswordCount = 100,
            PasswordAtRiskCount = 15,
            CriticalPasswordCount = 50,
            CriticalPasswordAtRiskCount = 5
        };

        // Act
        await sqlOrganizationReportRepo.UpdateMetricsAsync(report.Id, metrics);
        var updatedReport = await sqlOrganizationReportRepo.GetByIdAsync(report.Id);

        // Assert
        Assert.Equal(metrics.ApplicationCount, updatedReport.ApplicationCount);
        Assert.Equal(metrics.ApplicationAtRiskCount, updatedReport.ApplicationAtRiskCount);
        Assert.Equal(metrics.CriticalApplicationCount, updatedReport.CriticalApplicationCount);
        Assert.Equal(metrics.CriticalApplicationAtRiskCount, updatedReport.CriticalApplicationAtRiskCount);
        Assert.Equal(metrics.MemberCount, updatedReport.MemberCount);
        Assert.Equal(metrics.MemberAtRiskCount, updatedReport.MemberAtRiskCount);
        Assert.Equal(metrics.CriticalMemberCount, updatedReport.CriticalMemberCount);
        Assert.Equal(metrics.CriticalMemberAtRiskCount, updatedReport.CriticalMemberAtRiskCount);
        Assert.Equal(metrics.PasswordCount, updatedReport.PasswordCount);
        Assert.Equal(metrics.PasswordAtRiskCount, updatedReport.PasswordAtRiskCount);
        Assert.Equal(metrics.CriticalPasswordCount, updatedReport.CriticalPasswordCount);
        Assert.Equal(metrics.CriticalPasswordAtRiskCount, updatedReport.CriticalPasswordAtRiskCount);
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
