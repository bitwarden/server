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
        var (firstOrg, _) = await CreateOrganizationAndReportAsync(sqlOrganizationRepo, sqlOrganizationReportRepo);
        var (secondOrg, _) = await CreateOrganizationAndReportAsync(sqlOrganizationRepo, sqlOrganizationReportRepo);

        var firstSetOfRecords = await sqlOrganizationReportRepo.GetByOrganizationIdAsync(firstOrg.Id);
        var nextSetOfRecords = await sqlOrganizationReportRepo.GetByOrganizationIdAsync(secondOrg.Id);

        Assert.Equal(firstOrg.Id, firstSetOfRecords.OrganizationId);
        Assert.Equal(secondOrg.Id, nextSetOfRecords.OrganizationId);
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

        // Create a second report for the same organization
        var fixture = new Fixture();
        var secondReport = fixture.Build<OrganizationReport>()
            .With(x => x.OrganizationId, org.Id)
            .With(x => x.CreationDate, DateTime.UtcNow.AddMinutes(1))
            .Create();
        await sqlOrganizationReportRepo.CreateAsync(secondReport);

        // Act
        var latestReport = await sqlOrganizationReportRepo.GetLatestByOrganizationIdAsync(org.Id);

        // Assert
        Assert.NotNull(latestReport);
        Assert.Equal(org.Id, latestReport.OrganizationId);
        Assert.True(latestReport.CreationDate >= firstReport.CreationDate);
    }

    [CiSkippedTheory, EfOrganizationReportAutoData]
    public async Task UpdateSummaryDataAsync_ShouldUpdateSummaryAndRevisionDate(
        OrganizationReportRepository sqlOrganizationReportRepo,
        SqlRepo.OrganizationRepository sqlOrganizationRepo)
    {
        // Arrange
        var (_, report) = await CreateOrganizationAndReportAsync(sqlOrganizationRepo, sqlOrganizationReportRepo);
        var newSummaryData = "Updated summary data";
        var originalRevisionDate = report.RevisionDate;

        // Act
        var updatedReport = await sqlOrganizationReportRepo.UpdateSummaryDataAsync(report.Id, newSummaryData);

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
        var fixture = new Fixture();
        var summaryData = "Test summary data";
        var (org, _) = await CreateOrganizationAndReportWithSummaryDataAsync(
            sqlOrganizationRepo, sqlOrganizationReportRepo, summaryData);

        var report = await sqlOrganizationReportRepo.GetByOrganizationIdAsync(org.Id);

        // Act
        var result = await sqlOrganizationReportRepo.GetSummaryDataAsync(org.Id, report.Id);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(org.Id, result.OrganizationId);
        Assert.Equal(report.Id, result.Id);
        Assert.Equal(summaryData, result.SummaryData);
    }

    [CiSkippedTheory, EfOrganizationReportAutoData]
    public async Task GetSummaryDataByDateRangeAsync_ShouldReturnFilteredResults(
        OrganizationReportRepository sqlOrganizationReportRepo,
        SqlRepo.OrganizationRepository sqlOrganizationRepo)
    {
        // Arrange
        var (org, report1) = await CreateOrganizationAndReportWithSummaryDataAsync(
            sqlOrganizationRepo, sqlOrganizationReportRepo, "Summary 1");

        var fixture = new Fixture();
        var report2 = fixture.Build<OrganizationReport>()
            .With(x => x.OrganizationId, org.Id)
            .With(x => x.SummaryData, "Summary 2")
            .With(x => x.CreationDate, DateTime.UtcNow.AddDays(-5))
            .Create();
        await sqlOrganizationReportRepo.CreateAsync(report2);

        var startDate = DateTime.UtcNow.AddDays(-10);
        var endDate = DateTime.UtcNow.AddDays(1);

        // Act
        var results = await sqlOrganizationReportRepo.GetSummaryDataByDateRangeAsync(
            org.Id, report1.Id, startDate, endDate);

        // Assert
        Assert.NotNull(results);
        var resultsList = results.ToList();
        Assert.True(resultsList.Count > 0);
        Assert.All(resultsList, r => Assert.Equal(org.Id, r.OrganizationId));
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
        Assert.Equal(org.Id, result.OrganizationId);
        Assert.Equal(report.Id, result.Id);
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

        // Act
        var updatedReport = await sqlOrganizationReportRepo.UpdateReportDataAsync(
            org.Id, report.Id, newReportData);

        // Assert
        Assert.NotNull(updatedReport);
        Assert.Equal(org.Id, updatedReport.OrganizationId);
        Assert.Equal(report.Id, updatedReport.Id);
        Assert.True(updatedReport.RevisionDate > originalRevisionDate);
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
        Assert.Equal(org.Id, result.OrganizationId);
        Assert.Equal(report.Id, result.Id);
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

        // Act
        var updatedReport = await sqlOrganizationReportRepo.UpdateApplicationDataAsync(
            org.Id, report.Id, newApplicationData);

        // Assert
        Assert.NotNull(updatedReport);
        Assert.Equal(org.Id, updatedReport.OrganizationId);
        Assert.Equal(report.Id, updatedReport.Id);
        Assert.True(updatedReport.RevisionDate > originalRevisionDate);
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
