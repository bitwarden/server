using AutoFixture;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.Repositories;
using Bit.Core.Test.AutoFixture.Attributes;
using Bit.Core.Tools.Entities;
using Bit.Core.Tools.Repositories;
using Bit.Infrastructure.EFIntegration.Test.AutoFixture;
using Xunit;
using EfRepo = Bit.Infrastructure.EntityFramework.Repositories;
using EfToolsRepo = Bit.Infrastructure.EntityFramework.Tools.Repositories;
using SqlAdminConsoleRepo = Bit.Infrastructure.Dapper.Tools.Repositories;
using SqlRepo = Bit.Infrastructure.Dapper.Repositories;

namespace Bit.Infrastructure.EFIntegration.Test.Tools.Repositories;

public class PasswordHealthReportApplicationRepositoryTests
{
    [CiSkippedTheory, EfPasswordHealthReportApplicationAutoData]
    public async Task CreateAsync_Works_DataMatches(
        PasswordHealthReportApplication passwordHealthReportApplication,
        Organization organization,
        List<EfToolsRepo.PasswordHealthReportApplicationRepository> suts,
        List<EfRepo.OrganizationRepository> efOrganizationRepos,
        SqlAdminConsoleRepo.PasswordHealthReportApplicationRepository sqlPasswordHealthReportApplicationRepo,
        SqlRepo.OrganizationRepository sqlOrganizationRepo
        )
    {
        var passwordHealthReportApplicationRecords = new List<PasswordHealthReportApplication>();
        foreach (var sut in suts)
        {
            var i = suts.IndexOf(sut);

            var efOrganization = await efOrganizationRepos[i].CreateAsync(organization);
            sut.ClearChangeTracking();

            passwordHealthReportApplication.OrganizationId = efOrganization.Id;
            var postEfPasswordHeathReportApp = await sut.CreateAsync(passwordHealthReportApplication);
            sut.ClearChangeTracking();

            var savedPasswordHealthReportApplication = await sut.GetByIdAsync(postEfPasswordHeathReportApp.Id);
            passwordHealthReportApplicationRecords.Add(savedPasswordHealthReportApplication);
        }

        var sqlOrganization = await sqlOrganizationRepo.CreateAsync(organization);

        passwordHealthReportApplication.OrganizationId = sqlOrganization.Id;
        var sqlPasswordHealthReportApplicationRecord = await sqlPasswordHealthReportApplicationRepo.CreateAsync(passwordHealthReportApplication);
        var savedSqlPasswordHealthReportApplicationRecord = await sqlPasswordHealthReportApplicationRepo.GetByIdAsync(sqlPasswordHealthReportApplicationRecord.Id);
        passwordHealthReportApplicationRecords.Add(savedSqlPasswordHealthReportApplicationRecord);

        Assert.True(passwordHealthReportApplicationRecords.Count == 4);
    }

    [CiSkippedTheory, EfPasswordHealthReportApplicationAutoData]
    public async Task RetrieveByOrganisation_Works(
        SqlAdminConsoleRepo.PasswordHealthReportApplicationRepository sqlPasswordHealthReportApplicationRepo,
        SqlRepo.OrganizationRepository sqlOrganizationRepo)
    {
        var (firstOrg, firstRecord) = await CreateSampleRecord(sqlOrganizationRepo, sqlPasswordHealthReportApplicationRepo);
        var (secondOrg, secondRecord) = await CreateSampleRecord(sqlOrganizationRepo, sqlPasswordHealthReportApplicationRepo);

        var firstSetOfRecords = await sqlPasswordHealthReportApplicationRepo.GetByOrganizationIdAsync(firstOrg.Id);
        var nextSetOfRecords = await sqlPasswordHealthReportApplicationRepo.GetByOrganizationIdAsync(secondOrg.Id);

        Assert.True(firstSetOfRecords.Count == 1 && firstSetOfRecords.First().OrganizationId == firstOrg.Id);
        Assert.True(nextSetOfRecords.Count == 1 && nextSetOfRecords.First().OrganizationId == secondOrg.Id);
    }

    [CiSkippedTheory, EfPasswordHealthReportApplicationAutoData]
    public async Task ReplaceQuery_Works(
        List<EfToolsRepo.PasswordHealthReportApplicationRepository> suts,
        List<EfRepo.OrganizationRepository> efOrganizationRepos,
        SqlAdminConsoleRepo.PasswordHealthReportApplicationRepository sqlPasswordHealthReportApplicationRepo,
        SqlRepo.OrganizationRepository sqlOrganizationRepo)
    {
        var (org, pwdRecord) = await CreateSampleRecord(sqlOrganizationRepo, sqlPasswordHealthReportApplicationRepo);
        var exampleUri = "http://www.example.com";
        var exampleRevisionDate = new DateTime(2021, 1, 1);
        var dbRecords = new List<PasswordHealthReportApplication>();

        foreach (var sut in suts)
        {
            var i = suts.IndexOf(sut);

            // create a new organization for each repository
            var organization = await efOrganizationRepos[i].CreateAsync(org);

            // map the organization Id and create the PasswordHealthReportApp record
            pwdRecord.OrganizationId = organization.Id;
            var passwordHealthReportApplication = await sut.CreateAsync(pwdRecord);

            // update the record with new values
            passwordHealthReportApplication.Uri = exampleUri;
            passwordHealthReportApplication.RevisionDate = exampleRevisionDate;

            // apply update to the database
            await sut.ReplaceAsync(passwordHealthReportApplication);
            sut.ClearChangeTracking();

            // retrieve the data and add to the list for assertions
            var recordFromDb = await sut.GetByIdAsync(passwordHealthReportApplication.Id);
            sut.ClearChangeTracking();

            dbRecords.Add(recordFromDb);
        }

        // sql - create a new organization and PasswordHealthReportApplication record
        var (sqlOrg, sqlPwdRecord) = await CreateSampleRecord(sqlOrganizationRepo, sqlPasswordHealthReportApplicationRepo);
        var sqlPasswordHealthReportApplicationRecord = await sqlPasswordHealthReportApplicationRepo.GetByIdAsync(sqlPwdRecord.Id);

        // sql - update the record with new values
        sqlPasswordHealthReportApplicationRecord.Uri = exampleUri;
        sqlPasswordHealthReportApplicationRecord.RevisionDate = exampleRevisionDate;
        await sqlPasswordHealthReportApplicationRepo.ReplaceAsync(sqlPasswordHealthReportApplicationRecord);

        // sql - retrieve the data and add to the list for assertions
        var sqlDbRecord = await sqlPasswordHealthReportApplicationRepo.GetByIdAsync(sqlPasswordHealthReportApplicationRecord.Id);
        dbRecords.Add(sqlDbRecord);

        // assertions
        // the Guids must be distinct across all records
        Assert.True(dbRecords.Select(_ => _.Id).Distinct().Count() == dbRecords.Count);

        // the Uri and RevisionDate must match the updated values
        Assert.True(dbRecords.All(_ => _.Uri == exampleUri && _.RevisionDate == exampleRevisionDate));
    }

    [CiSkippedTheory, EfPasswordHealthReportApplicationAutoData]
    public async Task Upsert_Works(
        List<EfToolsRepo.PasswordHealthReportApplicationRepository> suts,
        List<EfRepo.OrganizationRepository> efOrganizationRepos,
        SqlAdminConsoleRepo.PasswordHealthReportApplicationRepository sqlPasswordHealthReportApplicationRepo,
        SqlRepo.OrganizationRepository sqlOrganizationRepo)
    {
        var fixture = new Fixture();
        var rawOrg = fixture.Build<Organization>().Create();
        var rawPwdRecord = fixture.Build<PasswordHealthReportApplication>()
                            .With(_ => _.OrganizationId, rawOrg.Id)
                            .Without(_ => _.Id)
                            .Create();
        var exampleUri = "http://www.example.com";
        var exampleRevisionDate = new DateTime(2021, 1, 1);
        var dbRecords = new List<PasswordHealthReportApplication>();

        foreach (var sut in suts)
        {
            var i = suts.IndexOf(sut);

            // create a new organization for each repository
            var organization = await efOrganizationRepos[i].CreateAsync(rawOrg);

            // map the organization Id and use Upsert to save new record
            rawPwdRecord.OrganizationId = organization.Id;
            rawPwdRecord.Id = default(Guid);
            await sut.UpsertAsync(rawPwdRecord);
            sut.ClearChangeTracking();

            // retrieve the data and add to the list for assertions
            var passwordHealthReportApplication = await sut.GetByIdAsync(rawPwdRecord.Id);

            // update the record with new values
            passwordHealthReportApplication.Uri = exampleUri;
            passwordHealthReportApplication.RevisionDate = exampleRevisionDate;

            // apply update using Upsert to make changes to db
            await sut.UpsertAsync(passwordHealthReportApplication);
            sut.ClearChangeTracking();

            // retrieve the data and add to the list for assertions
            var recordFromDb = await sut.GetByIdAsync(passwordHealthReportApplication.Id);
            dbRecords.Add(recordFromDb);

            sut.ClearChangeTracking();
        }

        // sql - create new records
        var organizationForSql = fixture.Create<Organization>();
        var passwordHealthReportApplicationForSql = fixture.Build<PasswordHealthReportApplication>()
            .With(_ => _.OrganizationId, organizationForSql.Id)
            .Without(_ => _.Id)
            .Create();

        // sql - use Upsert to insert this data
        var sqlOrganization = await sqlOrganizationRepo.CreateAsync(organizationForSql);
        await sqlPasswordHealthReportApplicationRepo.UpsertAsync(passwordHealthReportApplicationForSql);
        var sqlPasswordHealthReportApplicationRecord = await sqlPasswordHealthReportApplicationRepo.GetByIdAsync(passwordHealthReportApplicationForSql.Id);

        // sql - update the record with new values
        sqlPasswordHealthReportApplicationRecord.Uri = exampleUri;
        sqlPasswordHealthReportApplicationRecord.RevisionDate = exampleRevisionDate;
        await sqlPasswordHealthReportApplicationRepo.UpsertAsync(sqlPasswordHealthReportApplicationRecord);

        // sql - retrieve the data and add to the list for assertions
        var sqlDbRecord = await sqlPasswordHealthReportApplicationRepo.GetByIdAsync(sqlPasswordHealthReportApplicationRecord.Id);
        dbRecords.Add(sqlDbRecord);

        // assertions
        // the Guids must be distinct across all records
        Assert.True(dbRecords.Select(_ => _.Id).Distinct().Count() == dbRecords.Count);

        // the Uri and RevisionDate must match the updated values
        Assert.True(dbRecords.All(_ => _.Uri == exampleUri && _.RevisionDate == exampleRevisionDate));
    }

    [CiSkippedTheory, EfPasswordHealthReportApplicationAutoData]
    public async Task Delete_Works(
        List<EfToolsRepo.PasswordHealthReportApplicationRepository> suts,
        List<EfRepo.OrganizationRepository> efOrganizationRepos,
        SqlAdminConsoleRepo.PasswordHealthReportApplicationRepository sqlPasswordHealthReportApplicationRepo,
        SqlRepo.OrganizationRepository sqlOrganizationRepo)
    {
        var fixture = new Fixture();
        var rawOrg = fixture.Build<Organization>().Create();
        var rawPwdRecord = fixture.Build<PasswordHealthReportApplication>()
                            .With(_ => _.OrganizationId, rawOrg.Id)
                            .Create();
        var dbRecords = new List<PasswordHealthReportApplication>();

        foreach (var sut in suts)
        {
            var i = suts.IndexOf(sut);

            // create a new organization for each repository
            var organization = await efOrganizationRepos[i].CreateAsync(rawOrg);

            // map the organization Id and use Upsert to save new record
            rawPwdRecord.OrganizationId = organization.Id;
            rawPwdRecord = await sut.CreateAsync(rawPwdRecord);
            sut.ClearChangeTracking();

            // apply update using Upsert to make changes to db
            await sut.DeleteAsync(rawPwdRecord);
            sut.ClearChangeTracking();

            // retrieve the data and add to the list for assertions
            var recordFromDb = await sut.GetByIdAsync(rawPwdRecord.Id);
            dbRecords.Add(recordFromDb);

            sut.ClearChangeTracking();
        }

        // sql - create new records
        var (org, passwordHealthReportApplication) = await CreateSampleRecord(sqlOrganizationRepo, sqlPasswordHealthReportApplicationRepo);
        await sqlPasswordHealthReportApplicationRepo.DeleteAsync(passwordHealthReportApplication);
        var sqlDbRecord = await sqlPasswordHealthReportApplicationRepo.GetByIdAsync(passwordHealthReportApplication.Id);
        dbRecords.Add(sqlDbRecord);

        // assertions
        // all records should be null - as they were deleted before querying
        Assert.True(dbRecords.Where(_ => _ == null).Count() == 4);
    }

    private async Task<(Organization, PasswordHealthReportApplication)> CreateSampleRecord(
        IOrganizationRepository organizationRepo,
        IPasswordHealthReportApplicationRepository passwordHealthReportApplicationRepo
    )
    {
        var fixture = new Fixture();
        var organization = fixture.Create<Organization>();
        var passwordHealthReportApplication = fixture.Build<PasswordHealthReportApplication>()
            .With(_ => _.OrganizationId, organization.Id)
            .Create();

        organization = await organizationRepo.CreateAsync(organization);
        passwordHealthReportApplication = await passwordHealthReportApplicationRepo.CreateAsync(passwordHealthReportApplication);

        return (organization, passwordHealthReportApplication);
    }
}
