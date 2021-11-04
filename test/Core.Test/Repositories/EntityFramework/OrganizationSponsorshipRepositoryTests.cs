using EfRepo = Bit.Core.Repositories.EntityFramework;
using SqlRepo = Bit.Core.Repositories.SqlServer;
using System.Collections.Generic;
using System.Linq;
using TableModel = Bit.Core.Models.Table;
using Xunit;
using Bit.Core.Test.Repositories.EntityFramework.EqualityComparers;
using Bit.Core.Test.AutoFixture.Attributes;
using Bit.Core.Test.AutoFixture.OrganizationSponsorshipFixtures;

namespace Bit.Core.Test.Repositories.EntityFramework
{
    public class OrganizationSponsorshipRepositoryTests
    {
        [CiSkippedTheory, EfOrganizationSponsorshipAutoData]
        public async void CreateAsync_Works_DataMatches(
            TableModel.OrganizationSponsorship organizationSponsorship, TableModel.Organization sponsoringOrg,
            List<EfRepo.OrganizationRepository> efOrgRepos,
            SqlRepo.OrganizationRepository sqlOrganizationRepo,
            SqlRepo.OrganizationSponsorshipRepository sqlOrganizationSponsorshipRepo,
            OrganizationSponsorshipCompare equalityComparer,
            List<EfRepo.OrganizationSponsorshipRepository> suts)
        {
            organizationSponsorship.InstallationId = null;
            organizationSponsorship.SponsoredOrganizationId = null;

            var savedOrganizationSponsorships = new List<TableModel.OrganizationSponsorship>();
            foreach (var (sut, orgRepo) in suts.Zip(efOrgRepos))
            {
                var efSponsoringOrg = await orgRepo.CreateAsync(sponsoringOrg);
                sut.ClearChangeTracking();
                organizationSponsorship.SponsoringOrganizationId = efSponsoringOrg.Id;

                await sut.CreateAsync(organizationSponsorship);
                sut.ClearChangeTracking();

                var savedOrganizationSponsorship = await sut.GetByIdAsync(organizationSponsorship.Id);
                savedOrganizationSponsorships.Add(savedOrganizationSponsorship);
            }

            var sqlSponsoringOrg = await sqlOrganizationRepo.CreateAsync(sponsoringOrg);
            organizationSponsorship.SponsoringOrganizationId = sqlSponsoringOrg.Id;

            var sqlOrganizationSponsorship = await sqlOrganizationSponsorshipRepo.CreateAsync(organizationSponsorship);
            savedOrganizationSponsorships.Add(await sqlOrganizationSponsorshipRepo.GetByIdAsync(sqlOrganizationSponsorship.Id));

            var distinctItems = savedOrganizationSponsorships.Distinct(equalityComparer);
            Assert.True(!distinctItems.Skip(1).Any());
        }

        [CiSkippedTheory, EfOrganizationSponsorshipAutoData]
        public async void ReplaceAsync_Works_DataMatches(TableModel.OrganizationSponsorship postOrganizationSponsorship,
            TableModel.OrganizationSponsorship replaceOrganizationSponsorship, TableModel.Organization sponsoringOrg,
            List<EfRepo.OrganizationRepository> efOrgRepos,
            SqlRepo.OrganizationRepository sqlOrganizationRepo,
            SqlRepo.OrganizationSponsorshipRepository sqlOrganizationSponsorshipRepo,
            OrganizationSponsorshipCompare equalityComparer, List<EfRepo.OrganizationSponsorshipRepository> suts)
        {
            postOrganizationSponsorship.InstallationId = null;
            postOrganizationSponsorship.SponsoredOrganizationId = null;
            replaceOrganizationSponsorship.InstallationId = null;
            replaceOrganizationSponsorship.SponsoredOrganizationId = null;

            var savedOrganizationSponsorships = new List<TableModel.OrganizationSponsorship>();
            foreach (var (sut, orgRepo) in suts.Zip(efOrgRepos))
            {
                var efSponsoringOrg = await orgRepo.CreateAsync(sponsoringOrg);
                sut.ClearChangeTracking();
                postOrganizationSponsorship.SponsoringOrganizationId = efSponsoringOrg.Id;
                replaceOrganizationSponsorship.SponsoringOrganizationId = efSponsoringOrg.Id;

                var postEfOrganizationSponsorship = await sut.CreateAsync(postOrganizationSponsorship);
                sut.ClearChangeTracking();

                replaceOrganizationSponsorship.Id = postEfOrganizationSponsorship.Id;
                await sut.ReplaceAsync(replaceOrganizationSponsorship);
                sut.ClearChangeTracking();

                var replacedOrganizationSponsorship = await sut.GetByIdAsync(replaceOrganizationSponsorship.Id);
                savedOrganizationSponsorships.Add(replacedOrganizationSponsorship);
            }

            var sqlSponsoringOrg = await sqlOrganizationRepo.CreateAsync(sponsoringOrg);
            postOrganizationSponsorship.SponsoringOrganizationId = sqlSponsoringOrg.Id;

            var postSqlOrganization = await sqlOrganizationSponsorshipRepo.CreateAsync(postOrganizationSponsorship);
            replaceOrganizationSponsorship.Id = postSqlOrganization.Id;
            await sqlOrganizationSponsorshipRepo.ReplaceAsync(replaceOrganizationSponsorship);
            savedOrganizationSponsorships.Add(await sqlOrganizationSponsorshipRepo.GetByIdAsync(replaceOrganizationSponsorship.Id));

            var distinctItems = savedOrganizationSponsorships.Distinct(equalityComparer);
            Assert.True(!distinctItems.Skip(1).Any());
        }

        [CiSkippedTheory, EfOrganizationSponsorshipAutoData]
        public async void DeleteAsync_Works_DataMatches(TableModel.OrganizationSponsorship organizationSponsorship,
            TableModel.Organization sponsoringOrg,
            List<EfRepo.OrganizationRepository> efOrgRepos,
            SqlRepo.OrganizationRepository sqlOrganizationRepo,
            SqlRepo.OrganizationSponsorshipRepository sqlOrganizationSponsorshipRepo,
            List<EfRepo.OrganizationSponsorshipRepository> suts)
        {
            organizationSponsorship.InstallationId = null;
            organizationSponsorship.SponsoredOrganizationId = null;

            foreach (var (sut, orgRepo) in suts.Zip(efOrgRepos))
            {
                var efSponsoringOrg = await orgRepo.CreateAsync(sponsoringOrg);
                sut.ClearChangeTracking();
                organizationSponsorship.SponsoringOrganizationId = efSponsoringOrg.Id;

                var postEfOrganizationSponsorship = await sut.CreateAsync(organizationSponsorship);
                sut.ClearChangeTracking();

                var savedEfOrganizationSponsorship = await sut.GetByIdAsync(postEfOrganizationSponsorship.Id);
                sut.ClearChangeTracking();
                Assert.True(savedEfOrganizationSponsorship != null);

                await sut.DeleteAsync(savedEfOrganizationSponsorship);
                sut.ClearChangeTracking();

                savedEfOrganizationSponsorship = await sut.GetByIdAsync(savedEfOrganizationSponsorship.Id);
                Assert.True(savedEfOrganizationSponsorship == null);
            }

            var sqlSponsoringOrg = await sqlOrganizationRepo.CreateAsync(sponsoringOrg);
            organizationSponsorship.SponsoringOrganizationId = sqlSponsoringOrg.Id;

            var postSqlOrganizationSponsorship = await sqlOrganizationSponsorshipRepo.CreateAsync(organizationSponsorship);
            var savedSqlOrganizationSponsorship = await sqlOrganizationSponsorshipRepo.GetByIdAsync(postSqlOrganizationSponsorship.Id);
            Assert.True(savedSqlOrganizationSponsorship != null);

            await sqlOrganizationSponsorshipRepo.DeleteAsync(postSqlOrganizationSponsorship);
            savedSqlOrganizationSponsorship = await sqlOrganizationSponsorshipRepo.GetByIdAsync(postSqlOrganizationSponsorship.Id);
            Assert.True(savedSqlOrganizationSponsorship == null);
        }
    }
}
