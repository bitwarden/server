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
        var sqlOrgnizationReportRecord = await sqlOrganizationReportRepo.CreateAsync(report);
        var savedSqlOrganizationReport = await sqlOrganizationReportRepo.GetByIdAsync(sqlOrgnizationReportRecord.Id);
        records.Add(savedSqlOrganizationReport);

        Assert.True(records.Count == 4);
    }

    [CiSkippedTheory, EfOrganizationReportAutoData]
    public async Task RetrieveByOrganisation_Works(
        OrganizationReportRepository sqlPasswordHealthReportApplicationRepo,
        SqlRepo.OrganizationRepository sqlOrganizationRepo)
    {
        var (firstOrg, _) = await CreateOrganizationAndReportAsync(sqlOrganizationRepo, sqlPasswordHealthReportApplicationRepo);
        var (secondOrg, _) = await CreateOrganizationAndReportAsync(sqlOrganizationRepo, sqlPasswordHealthReportApplicationRepo);

        var firstSetOfRecords = await sqlPasswordHealthReportApplicationRepo.GetByOrganizationIdAsync(firstOrg.Id);
        var nextSetOfRecords = await sqlPasswordHealthReportApplicationRepo.GetByOrganizationIdAsync(secondOrg.Id);

        Assert.True(firstSetOfRecords.Count == 1 && firstSetOfRecords.First().OrganizationId == firstOrg.Id);
        Assert.True(nextSetOfRecords.Count == 1 && nextSetOfRecords.First().OrganizationId == secondOrg.Id);
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
}
