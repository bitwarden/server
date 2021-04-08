using Bit.Core.Test.AutoFixture;
using Bit.Core.Test.Helpers.Factories;
using EfRepo = Bit.Core.Repositories.EntityFramework;
using SqlRepo = Bit.Core.Repositories.SqlServer;
using System.Collections.Generic;
using System.Linq;
using TableModel = Bit.Core.Models.Table;
using DataModel = Bit.Core.Models.Data;
using Xunit;
using Bit.Core.Test.Repositories.EntityFramework.EqualityComparers;
using Bit.Core.Test.AutoFixture.OrganizationFixtures;

namespace Bit.Core.Test.Repositories.EntityFramework
{
    public class OrganizationRepositoryTests
    {
        [Theory, EfOrganizationAutoData]
        public async void CreateAsync_Works_DataMatches(TableModel.Organization organization,
                SqlRepo.OrganizationRepository sqlOrganizationRepo, OrganizationCompare equalityComparer,
                SutProvider<EfRepo.OrganizationRepository> sutProvider)
        {
            var savedOrganizations = new List<TableModel.Organization>();
            foreach (var option in DatabaseOptionsFactory.Options)
            {
                using (var context = new EfRepo.DatabaseContext(option))
                {
                    context.Database.EnsureDeleted();
                    context.Database.EnsureCreated();
                    var postEfOrganization = await sutProvider.Sut.CreateAsync(context, organization);
                    var savedOrganization = await sutProvider.Sut.GetByIdAsync(context, organization.Id);
                    savedOrganizations.Add(savedOrganization);
                }
            }

            var sqlUser = await sqlOrganizationRepo.CreateAsync(organization);
            savedOrganizations.Add(await sqlOrganizationRepo.GetByIdAsync(sqlUser.Id));

            var distinctItems = savedOrganizations.Distinct(equalityComparer);
            Assert.True(!distinctItems.Skip(1).Any());
        }

        [Theory, EfOrganizationAutoData]
        public async void ReplaceAsync_Works_DataMatches(TableModel.Organization postOrganization,
                TableModel.Organization replaceOrganization, SqlRepo.OrganizationRepository sqlOrganizationRepo,
                OrganizationCompare equalityComparer, SutProvider<EfRepo.OrganizationRepository> sutProvider)
        {
            var savedOrganizations = new List<TableModel.Organization>();
            foreach (var option in DatabaseOptionsFactory.Options)
            {
                using (var context = new EfRepo.DatabaseContext(option))
                {
                    var postEfOrganization = await sutProvider.Sut.CreateAsync(context, postOrganization);
                    replaceOrganization.Id = postEfOrganization.Id;
                    await sutProvider.Sut.ReplaceAsync(context, replaceOrganization);
                    var replacedOrganization = await sutProvider.Sut.GetByIdAsync(context, replaceOrganization.Id);
                    savedOrganizations.Add(replacedOrganization);
                }
            }

            var postSqlOrganization = await sqlOrganizationRepo.CreateAsync(postOrganization);
            replaceOrganization.Id = postSqlOrganization.Id;
            await sqlOrganizationRepo.ReplaceAsync(replaceOrganization);
            savedOrganizations.Add(await sqlOrganizationRepo.GetByIdAsync(replaceOrganization.Id));

            var distinctItems = savedOrganizations.Distinct(equalityComparer);
            Assert.True(!distinctItems.Skip(1).Any());
        }

        [Theory, EfOrganizationAutoData]
        public async void DeleteAsync_Works_DataMatches(TableModel.Organization organization,
                SqlRepo.OrganizationRepository sqlOrganizationRepo, OrganizationCompare equalityComparer, 
                SutProvider<EfRepo.OrganizationRepository> sutProvider)
        {
            foreach (var option in DatabaseOptionsFactory.Options)
            {
                TableModel.Organization savedEfOrganization = null;
                using (var context = new EfRepo.DatabaseContext(option))
                {
                    var postEfOrganization = await sutProvider.Sut.CreateAsync(context, organization);
                    savedEfOrganization = await sutProvider.Sut.GetByIdAsync(context, postEfOrganization.Id);
                    Assert.True(savedEfOrganization != null);
                }

                using (var context = new EfRepo.DatabaseContext(option))
                {
                    await sutProvider.Sut.DeleteAsync(context, savedEfOrganization);
                    savedEfOrganization = await sutProvider.Sut.GetByIdAsync(context, savedEfOrganization.Id);
                    Assert.True(savedEfOrganization == null);
                }
            }

            var postSqlOrganization = await sqlOrganizationRepo.CreateAsync(organization);
            var savedSqlOrganization = await sqlOrganizationRepo.GetByIdAsync(postSqlOrganization.Id);
            Assert.True(savedSqlOrganization != null);

            await sqlOrganizationRepo.DeleteAsync(postSqlOrganization);
            savedSqlOrganization = await sqlOrganizationRepo.GetByIdAsync(postSqlOrganization.Id);
            Assert.True(savedSqlOrganization == null);
        }

        [Theory, EfOrganizationAutoData]
        public async void GetByIdentifierAsync_Works_DataMatches(TableModel.Organization organization,
                SqlRepo.OrganizationRepository sqlOrganizationRepo, OrganizationCompare equalityComparer, 
                SutProvider<EfRepo.OrganizationRepository> sutProvider)
        {
            var returnedOrgs = new List<TableModel.Organization>();
            foreach (var option in DatabaseOptionsFactory.Options)
            {
                using (var context = new EfRepo.DatabaseContext(option))
                {
                    var postEfOrg = await sutProvider.Sut.CreateAsync(context, organization);
                    var returnedOrg = await sutProvider.Sut.GetByIdentifierAsync(context, postEfOrg.Identifier);
                    returnedOrgs.Add(returnedOrg);
                }
            }

            var postSqlOrg = await sqlOrganizationRepo.CreateAsync(organization);
            returnedOrgs.Add(await sqlOrganizationRepo.GetByIdentifierAsync(postSqlOrg.Identifier));

            var distinctItems = returnedOrgs.Distinct(equalityComparer);
            Assert.True(!distinctItems.Skip(1).Any());
        }

        [Theory, EfOrganizationAutoData]
        public async void GetManyByEnabledAsync_Works_DataMatches(TableModel.Organization organization,
                SqlRepo.OrganizationRepository sqlOrganizationRepo, OrganizationCompare equalityCompare, 
                SutProvider<EfRepo.OrganizationRepository> sutProvider)
        {
            var returnedOrgs = new List<TableModel.Organization>();
            foreach (var option in DatabaseOptionsFactory.Options)
            {
                using (var context = new EfRepo.DatabaseContext(option))
                {
                    var postEfOrg = await sutProvider.Sut.CreateAsync(context, organization);
                    var efReturnedOrgs = await sutProvider.Sut.GetManyByEnabledAsync(context);
                    returnedOrgs.Concat(efReturnedOrgs);
                }
            }

            var postSqlOrg = await sqlOrganizationRepo.CreateAsync(organization);
            returnedOrgs.Concat(await sqlOrganizationRepo.GetManyByEnabledAsync());

            Assert.True(returnedOrgs.All(o => o.Enabled));
        }

        [Theory, EfOrganizationAutoData]
        public async void GetManyByUserIdAsync_Works_DataMatches(TableModel.Organization organization,
                SqlRepo.OrganizationRepository sqlOrganizationRepo, OrganizationCompare equalityComparer, 
                SutProvider<EfRepo.OrganizationRepository> sutProvider)
        {
            // TODO: OrgUser repo needed
            Assert.True(true);
        }

        [Theory, EfOrganizationAutoData]
        public async void SearchAsync_Works_DataMatches(TableModel.Organization organization,
                SqlRepo.OrganizationRepository sqlOrganizationRepo, OrganizationCompare equalityCompare, 
                SutProvider<EfRepo.OrganizationRepository> sutProvider)
        {
            // TODO: OrgUser repo needed
            Assert.True(true);
        }

        [Theory, EfOrganizationAutoData]
        public async void UpdateStorageAsync_Works_DataMatches(TableModel.Organization organization,
                SqlRepo.OrganizationRepository sqlOrganizationRepo, OrganizationCompare equalityComparer, 
                SutProvider<EfRepo.OrganizationRepository> sutProvider)
        {
            // TODO: Cipher repo needed
            Assert.True(true);
        }

        [Theory, EfOrganizationAutoData]
        public async void GetManyAbilitiesAsync_Works_DataMatches(TableModel.Organization organization,
                SqlRepo.OrganizationRepository sqlOrganizationRepo, OrganizationCompare equalityComparer, 
                SutProvider<EfRepo.OrganizationRepository> sutProvider)
        {
            var list = new List<DataModel.OrganizationAbility>();
            foreach (var option in DatabaseOptionsFactory.Options)
            {
                using (var context = new EfRepo.DatabaseContext(option))
                {
                    list.Concat(await sutProvider.Sut.GetManyAbilitiesAsync(context));
                }
            }

            list.Concat(await sqlOrganizationRepo.GetManyAbilitiesAsync());
            Assert.True(list.All(x => x.GetType() == typeof(DataModel.OrganizationAbility)));
        }
    }
}
