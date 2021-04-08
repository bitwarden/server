using Bit.Core.Test.AutoFixture;
using Bit.Core.Test.Helpers.Factories;
using EfRepo = Bit.Core.Repositories.EntityFramework;
using SqlRepo = Bit.Core.Repositories.SqlServer;
using System.Collections.Generic;
using System.Linq;
using TableModel = Bit.Core.Models.Table;
using Xunit;
using Bit.Core.Test.Repositories.EntityFramework.EqualityComparers;
using Bit.Core.Test.AutoFixture.SsoConfigFixtures;
using System;

namespace Bit.Core.Test.Repositories.EntityFramework
{
    public class SsoConfigRepositoryTests
    {
        [Theory, EfSsoConfigAutoData]
        public async void CreateAsync_Works_DataMatches(TableModel.SsoConfig ssoConfig, SqlRepo.SsoConfigRepository sqlSsoConfigRepo,
                SsoConfigCompare equalityComparer, SutProvider<EfRepo.SsoConfigRepository> sutProvider, SutProvider<EfRepo.OrganizationRepository> orgRepoSut,
                TableModel.Organization org, SqlRepo.OrganizationRepository sqlOrganizationRepo)
        {
            // init list to hold one instance of the tested object from each database provider
            var savedSsoConfigs = new List<TableModel.SsoConfig>();

            foreach (var option in DatabaseOptionsFactory.Options)
            {
                // satisfy any foreign key constraints for the tested object
                using (var context = new EfRepo.DatabaseContext(option))
                {
                    var savedEfOrg = await orgRepoSut.Sut.CreateAsync(context, org);
                    ssoConfig.OrganizationId = savedEfOrg.Id;
                }

                // save the tested object & assert its existance
                using (var context = new EfRepo.DatabaseContext(option))
                {
                    var postEfSsoConfig = await sutProvider.Sut.CreateAsync(context, ssoConfig);
                    var savedEfSsoConfig = await sutProvider.Sut.GetByIdAsync(context, ssoConfig.Id);
                    Assert.True(savedEfSsoConfig != null);
                    savedSsoConfigs.Add(savedEfSsoConfig);
                }
            }

            //satisfy any foreign key constraints for the tested object
            var sqlOrganization = await sqlOrganizationRepo.CreateAsync(org);
            ssoConfig.OrganizationId = sqlOrganization.Id;

            // save the tested object and assert its existance
            var sqlSsoConfig = await sqlSsoConfigRepo.CreateAsync(ssoConfig);
            var savedSqlSsoConfig = await sqlSsoConfigRepo.GetByIdAsync(sqlSsoConfig.Id);
            Assert.True(savedSqlSsoConfig != null);
            savedSsoConfigs.Add(savedSqlSsoConfig);

            // assert all saved objects contain the same user relevant data
            var distinctItems = savedSsoConfigs.Distinct(equalityComparer);
            Assert.True(!distinctItems.Skip(1).Any());
        }

        [Theory, EfSsoConfigAutoData]
        public async void ReplaceAsync_Works_DataMatches(TableModel.SsoConfig postSsoConfig, TableModel.SsoConfig replaceSsoConfig, 
                SqlRepo.SsoConfigRepository sqlSsoConfigRepo, SsoConfigCompare equalityComparer, SutProvider<EfRepo.SsoConfigRepository> sutProvider,
                SutProvider<EfRepo.OrganizationRepository> orgRepoSut, TableModel.Organization org, SqlRepo.OrganizationRepository sqlOrganizationRepo)
        {
            // init list to hold an instance of the tested object from each database provider
            var savedSsoConfigs = new List<TableModel.SsoConfig>();

            foreach (var option in DatabaseOptionsFactory.Options)
            {
                // satisfy any foreign key constraints for the tested object
                using (var context = new EfRepo.DatabaseContext(option))
                {
                    var savedEfOrg = await orgRepoSut.Sut.CreateAsync(context, org);
                    postSsoConfig.OrganizationId = replaceSsoConfig.OrganizationId = savedEfOrg.Id;
                }

                // save the tested object
                using (var context = new EfRepo.DatabaseContext(option))
                {
                    var postEfSsoConfig = await sutProvider.Sut.CreateAsync(context, postSsoConfig);
                    replaceSsoConfig.Id = postEfSsoConfig.Id;
                    savedSsoConfigs.Add(postEfSsoConfig);
                }

                // replace the tested object & assert its existance 
                using (var context = new EfRepo.DatabaseContext(option))
                {
                    await sutProvider.Sut.ReplaceAsync(context, replaceSsoConfig);
                    var replacedSsoConfig = await sutProvider.Sut.GetByIdAsync(context, replaceSsoConfig.Id);
                    Assert.True(replacedSsoConfig != null);
                    savedSsoConfigs.Add(replacedSsoConfig);
                }
            }

            // satisfy any foreign key constraints for the tested object
            var sqlOrganization = await sqlOrganizationRepo.CreateAsync(org);
            postSsoConfig.OrganizationId = sqlOrganization.Id;

            // save the tested object
            var postSqlSsoConfig = await sqlSsoConfigRepo.CreateAsync(postSsoConfig);
            replaceSsoConfig.Id = postSqlSsoConfig.Id;
            savedSsoConfigs.Add(postSqlSsoConfig);

            // replace the tested object & assert its existance
            await sqlSsoConfigRepo.ReplaceAsync(replaceSsoConfig);
            var replacedSqlSsoConfig = await sqlSsoConfigRepo.GetByIdAsync(replaceSsoConfig.Id);
            Assert.True(replacedSqlSsoConfig != null);
            savedSsoConfigs.Add(replacedSqlSsoConfig);

            // assert that the stored post models and replace models are different, but that user-relevant data matches across database providers
            var distinctItems = savedSsoConfigs.Distinct(equalityComparer);
            Assert.True(!distinctItems.Skip(2).Any());
        }

        [Theory, EfSsoConfigAutoData]
        public async void DeleteAsync_Works_DataMatches(TableModel.SsoConfig ssoConfig, SqlRepo.SsoConfigRepository sqlSsoConfigRepo, SsoConfigCompare equalityComparer,
                SutProvider<EfRepo.SsoConfigRepository> ssoConfigRepoSut, SutProvider<EfRepo.OrganizationRepository> orgRepoSut,
                TableModel.Organization org, SqlRepo.OrganizationRepository sqlOrganizationRepo)
        {
            foreach (var option in DatabaseOptionsFactory.Options)
            {
                // satisfy any foreign key constraints for the tested object
                using (var context = new EfRepo.DatabaseContext(option))
                {
                    var savedEfOrg = await orgRepoSut.Sut.CreateAsync(context, org);
                    ssoConfig.OrganizationId = savedEfOrg.Id;
                }

                // save the tested object & assert its existance
                TableModel.SsoConfig savedEfSsoConfig;
                using (var context = new EfRepo.DatabaseContext(option))
                {
                    var postEfSsoConfig = await ssoConfigRepoSut.Sut.CreateAsync(context, ssoConfig);
                    savedEfSsoConfig = await ssoConfigRepoSut.Sut.GetByIdAsync(context, postEfSsoConfig.Id);
                    Assert.True(savedEfSsoConfig != null);
                }

                // delete the tested object & assert its nonexistance
                using (var context = new EfRepo.DatabaseContext(option))
                {
                    await ssoConfigRepoSut.Sut.DeleteAsync(context, savedEfSsoConfig);
                    var deletedEfSsoConfig= await ssoConfigRepoSut.Sut.GetByIdAsync(context, savedEfSsoConfig.Id);
                    Assert.True(deletedEfSsoConfig == null);
                }
            }

            //init any objects that will be written to the database
            var sqlOrganization = await sqlOrganizationRepo.CreateAsync(org);
            ssoConfig.OrganizationId = sqlOrganization.Id;

            // save the tested object and assert its existance
            var postSqlSsoConfig = await sqlSsoConfigRepo.CreateAsync(ssoConfig);
            var savedSqlSsoConfig = await sqlSsoConfigRepo.GetByIdAsync(postSqlSsoConfig.Id);
            Assert.True(savedSqlSsoConfig != null);

            // delete the tested object and assert its nonexistance
            await sqlSsoConfigRepo.DeleteAsync(savedSqlSsoConfig);
            savedSqlSsoConfig = await sqlSsoConfigRepo.GetByIdAsync(postSqlSsoConfig.Id);
            Assert.True(savedSqlSsoConfig == null);
        }

        [Theory, EfSsoConfigAutoData]
        public async void GetByOrganizationIdAsync_Works_DataMatches(TableModel.SsoConfig ssoConfig, SqlRepo.SsoConfigRepository sqlSsoConfigRepo, SsoConfigCompare equalityComparer,
                SutProvider<EfRepo.SsoConfigRepository> sutProvider, SutProvider<EfRepo.OrganizationRepository> orgRepoSut, TableModel.Organization org, SqlRepo.OrganizationRepository sqlOrgRepo)
        {
            // init list to hold one instance of the tested object from each database provider
            var returnedList = new List<TableModel.SsoConfig>();

            foreach (var option in DatabaseOptionsFactory.Options)
            {
                // satisfy any foreign key constraints for the tested object
                TableModel.Organization savedEfOrg;
                using (var context = new EfRepo.DatabaseContext(option))
                {
                    savedEfOrg = await orgRepoSut.Sut.CreateAsync(context, org);
                    ssoConfig.OrganizationId = savedEfOrg.Id;
                }

                // save an instance of the tested object
                using (var context = new EfRepo.DatabaseContext(option))
                {
                    await sutProvider.Sut.CreateAsync(context, ssoConfig);
                }

                // retreive the previously saved instance and assert its existence
                using (var context = new EfRepo.DatabaseContext(option))
                {
                    var savedEfUser = await sutProvider.Sut.GetByOrganizationIdAsync(context, savedEfOrg.Id);
                    Assert.True(savedEfUser != null);
                    returnedList.Add(savedEfUser);
                }
            }

            // satisfy any foreign key constraints for the tested object
            var savedSqlOrg = await sqlOrgRepo.CreateAsync(org);
            ssoConfig.OrganizationId = savedSqlOrg.Id;

            // save an instance of the tested object
            var postSqlSsoConfig = await sqlSsoConfigRepo.CreateAsync(ssoConfig);

            // retrieve the previously saved instance and assert its existence
            var savedSqlSsoConfig = await sqlSsoConfigRepo.GetByOrganizationIdAsync(ssoConfig.OrganizationId);
            Assert.True(savedSqlSsoConfig != null);
            returnedList.Add(savedSqlSsoConfig);

            // assert all retrieved objects contain the same user relevant data
            var distinctItems = returnedList.Distinct(equalityComparer);
            Assert.True(!distinctItems.Skip(1).Any());
        }

        [Theory, EfSsoConfigAutoData]
        public async void GetByIdentifierAsync_Works_DataMatches(TableModel.SsoConfig ssoConfig, SqlRepo.SsoConfigRepository sqlSsoConfigRepo, SsoConfigCompare equalityComparer,
                SutProvider<EfRepo.SsoConfigRepository> ssoConfigRepoSut, SutProvider<EfRepo.OrganizationRepository> orgRepoSut, TableModel.Organization org,
                SqlRepo.OrganizationRepository sqlOrgRepo)
        {
            // init list to hold one instance of the tested object from each database provider
            var returnedList = new List<TableModel.SsoConfig>();

            foreach (var option in DatabaseOptionsFactory.Options)
            {
                // satisfy any foreign key constraints for the tested object
                using (var context = new EfRepo.DatabaseContext(option))
                {
                    var savedEfOrg = await orgRepoSut.Sut.CreateAsync(context, org);
                    ssoConfig.OrganizationId = savedEfOrg.Id;
                }

                // save an instance of the tested object
                using (var context = new EfRepo.DatabaseContext(option))
                {
                    await ssoConfigRepoSut.Sut.CreateAsync(context, ssoConfig);
                }

                // retrieve the previously saved instance and assert its existance
                using (var context = new EfRepo.DatabaseContext(option))
                {
                    var savedEfSsoConfig = await ssoConfigRepoSut.Sut.GetByIdentifierAsync(context, org.Identifier);
                    Assert.True(savedEfSsoConfig != null);
                    returnedList.Add(savedEfSsoConfig);
                }
            }

            // satisfy any foreign key constraints for the tested object
            var savedSqlOrg = await sqlOrgRepo.CreateAsync(org);
            ssoConfig.OrganizationId = savedSqlOrg.Id;

            // save an instance of the tested object
            var postSqlSsoConfig = await sqlSsoConfigRepo.CreateAsync(ssoConfig);

            // retrieve the previously saved instance and assert its existence
            var savedSqlSsoConfig = await sqlSsoConfigRepo.GetByIdentifierAsync(org.Identifier);
            Assert.True(savedSqlSsoConfig != null);
            returnedList.Add(savedSqlSsoConfig);

            // assert all retrieved objects contain the same user relevant data
            var distinctItems = returnedList.Distinct(equalityComparer);
            Assert.True(!distinctItems.Skip(1).Any());
        }

        [Theory, EfSsoConfigAutoData]
        public async void GetManyByRevisionNotBeforeDate_Works(TableModel.SsoConfig ssoConfig, SqlRepo.SsoConfigRepository sqlSsoConfigRepo, SsoConfigCompare equalityComparer,
                SutProvider<EfRepo.SsoConfigRepository> ssoConfigRepoSut, DateTime notBeforeDate, SutProvider<EfRepo.OrganizationRepository> orgRepoSut, TableModel.Organization org)
        {
            foreach (var option in DatabaseOptionsFactory.Options)
            {
                // satisfy any foreign key constraints for the tested object
                using (var context = new EfRepo.DatabaseContext(option))
                {
                    var savedEfOrg = await orgRepoSut.Sut.CreateAsync(context, org);
                    ssoConfig.OrganizationId = savedEfOrg.Id;
                }

                // save an instance of the tested object
                using (var context = new EfRepo.DatabaseContext(option))
                {
                    await ssoConfigRepoSut.Sut.CreateAsync(context, ssoConfig);
                }

                // retrieve data from repo and assert they all match the condition logic
                // we can't compare directly with SQL here without modifying data for the entire table
                using (var context = new EfRepo.DatabaseContext(option))
                {
                    var returnedEfSsoConfigs = await ssoConfigRepoSut.Sut.GetManyByRevisionNotBeforeDate(context, notBeforeDate);
                    Assert.True(returnedEfSsoConfigs.All(sc => sc.RevisionDate >= notBeforeDate));
                }
            }
        }
    }
}
