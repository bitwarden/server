using Bit.Core.Test.AutoFixture;
using Bit.Core.Test.Helpers.Factories;
using EfRepo = Bit.Core.Repositories.EntityFramework;
using SqlRepo = Bit.Core.Repositories.SqlServer;
using System.Collections.Generic;
using System.Linq;
using TableModel = Bit.Core.Models.Table;
using Xunit;
using Bit.Core.Test.Repositories.EntityFramework.EqualityComparers;
using Bit.Core.Test.AutoFixture.SsoUserFixtures;

namespace Bit.Core.Test.Repositories.EntityFramework
{
    public class SsoUserRepositoryTests
    {
        [Theory, EfSsoUserAutoData]
        public async void CreateAsync_Works_DataMatches(TableModel.SsoUser ssoUser, SqlRepo.SsoUserRepository sqlSsoUserRepo,
                SsoUserCompare equalityComparer, SutProvider<EfRepo.SsoUserRepository> ssoUserRepoSut,
                SutProvider<EfRepo.UserRepository> userRepoSut, SutProvider<EfRepo.OrganizationRepository> orgRepoSut,
                TableModel.User user, TableModel.Organization org, SqlRepo.UserRepository sqlUserRepo, SqlRepo.OrganizationRepository sqlOrgRepo)
        {
            // init list to hold an instance of the tested object from each database provider
            var createdSsoUsers = new List<TableModel.SsoUser>();
            foreach(var option in DatabaseOptionsFactory.Options)
            {
                // satisfy any foreign key constraints for the tested object
                using (var context = new EfRepo.DatabaseContext(option))
                {
                    var efUser = await userRepoSut.Sut.CreateAsync(context, user);
                    var efOrg = await orgRepoSut.Sut.CreateAsync(context, org);
                    ssoUser.UserId = efUser.Id;
                    ssoUser.OrganizationId = efOrg.Id;
                }

                // save the tested object & assert its existance
                using (var context = new EfRepo.DatabaseContext(option))
                {
                    var postEfSsoUser = await ssoUserRepoSut.Sut.CreateAsync(context, ssoUser);
                    var savedSsoUser = await ssoUserRepoSut.Sut.GetByIdAsync(context, ssoUser.Id);
                    createdSsoUsers.Add(savedSsoUser);
                }
            }

            //satisfy any foreign key constraints for the tested object
            var sqlUser = await sqlUserRepo.CreateAsync(user);
            var sqlOrganization = await sqlOrgRepo.CreateAsync(org);
            ssoUser.UserId = sqlUser.Id;
            ssoUser.OrganizationId = sqlOrganization.Id;

            // save the tested object and assert its existance
            var sqlSsoUser = await sqlSsoUserRepo.CreateAsync(ssoUser);
            createdSsoUsers.Add(await sqlSsoUserRepo.GetByIdAsync(sqlSsoUser.Id));

            // assert all saved objects contain the same user relevant data
            var distinctSsoUsers = createdSsoUsers.Distinct(equalityComparer);
            Assert.True(!distinctSsoUsers.Skip(1).Any());
        }

        [Theory, EfSsoUserAutoData]
        public async void ReplaceAsync_Works_DataMatches(TableModel.SsoUser postSsoUser, TableModel.SsoUser replaceSsoUser, 
                SqlRepo.SsoUserRepository sqlSsoUserRepo, SsoUserCompare equalityComparer, SutProvider<EfRepo.SsoUserRepository> sutProvider,
                SutProvider<EfRepo.UserRepository> userRepoSut, SutProvider<EfRepo.OrganizationRepository> orgRepoSut,
                TableModel.Organization org, TableModel.User user, SqlRepo.OrganizationRepository sqlOrgRepo, SqlRepo.UserRepository sqlUserRepo)
        {
            // 
            var savedSsoUsers = new List<TableModel.SsoUser>();
            foreach(var option in DatabaseOptionsFactory.Options)
            {
                using (var context = new EfRepo.DatabaseContext(option))
                {
                    var efUser = await userRepoSut.Sut.CreateAsync(context, user);
                    var efOrg = await orgRepoSut.Sut.CreateAsync(context, org);
                    postSsoUser.UserId = efUser.Id;
                    postSsoUser.OrganizationId = efOrg.Id;
                }

                using (var context = new EfRepo.DatabaseContext(option))
                {
                    var postEfSsoUser = await sutProvider.Sut.CreateAsync(context, postSsoUser);
                    replaceSsoUser.Id = postEfSsoUser.Id;
                    replaceSsoUser.UserId = postEfSsoUser.UserId;
                    replaceSsoUser.OrganizationId = postEfSsoUser.OrganizationId;
                    await sutProvider.Sut.ReplaceAsync(context, replaceSsoUser);
                    var replacedSsoUser = await sutProvider.Sut.GetByIdAsync(context, replaceSsoUser.Id);
                    savedSsoUsers.Add(replacedSsoUser);
                }
            }

            var sqlUser = await sqlUserRepo.CreateAsync(user);
            var sqlOrganization = await sqlOrgRepo.CreateAsync(org);
            postSsoUser.Id = 0;
            postSsoUser.UserId = sqlUser.Id;
            postSsoUser.OrganizationId = sqlOrganization.Id;
            var postSqlSsoUser = await sqlSsoUserRepo.CreateAsync(postSsoUser);

            replaceSsoUser.Id = postSqlSsoUser.Id;
            replaceSsoUser.UserId = postSqlSsoUser.UserId;
            replaceSsoUser.OrganizationId = postSqlSsoUser.OrganizationId;
            await sqlSsoUserRepo.ReplaceAsync(replaceSsoUser);

            savedSsoUsers.Add(await sqlSsoUserRepo.GetByIdAsync(replaceSsoUser.Id));

            var distinctItems = savedSsoUsers.Distinct(equalityComparer);
            Assert.True(!distinctItems.Skip(1).Any());
        }

        [Theory, EfSsoUserAutoData]
        public async void DeleteAsync_Works_DataMatches(TableModel.SsoUser ssoUser, SqlRepo.SsoUserRepository sqlSsoUserRepo, SsoUserCompare equalityComparer,
                SutProvider<EfRepo.SsoUserRepository> ssoUserRepoSut, SutProvider<EfRepo.UserRepository> userRepoSut, SutProvider<EfRepo.OrganizationRepository> orgRepoSut,
                TableModel.User user, TableModel.Organization org, SqlRepo.UserRepository sqlUserRepo, SqlRepo.OrganizationRepository sqlOrganizationRepo)
        {
            foreach(var option in DatabaseOptionsFactory.Options)
            {
                // satisfy any foreign key constraints for the tested object
                using (var context = new EfRepo.DatabaseContext(option))
                {
                    var savedEfUser = await userRepoSut.Sut.CreateAsync(context, user);
                    var savedEfOrg = await orgRepoSut.Sut.CreateAsync(context, org);
                    ssoUser.UserId = savedEfUser.Id;
                    ssoUser.OrganizationId = savedEfOrg.Id;
                }

                // save the tested object & assert its existance
                TableModel.SsoUser savedEfSsoUser = null;
                using (var context = new EfRepo.DatabaseContext(option))
                {
                    var postEfSsoUser = await ssoUserRepoSut.Sut.CreateAsync(context, ssoUser);
                    savedEfSsoUser = await ssoUserRepoSut.Sut.GetByIdAsync(context, postEfSsoUser.Id);
                    Assert.True(savedEfSsoUser != null);
                }

                // delete the tested object & assert its nonexistance
                using (var context = new EfRepo.DatabaseContext(option))
                {
                    await ssoUserRepoSut.Sut.DeleteAsync(context, savedEfSsoUser);
                    savedEfSsoUser = await ssoUserRepoSut.Sut.GetByIdAsync(context, savedEfSsoUser.Id);
                    Assert.True(savedEfSsoUser == null);
                }
            }

            //init any objects that will be written to the database
            var sqlUser = await sqlUserRepo.CreateAsync(user);
            var sqlOrganization = await sqlOrganizationRepo.CreateAsync(org);
            ssoUser.UserId = sqlUser.Id;
            ssoUser.OrganizationId = sqlOrganization.Id;

            // save the tested object and assert its existance
            var postSqlSsoUser = await sqlSsoUserRepo.CreateAsync(ssoUser);
            var savedSqlSsoUser = await sqlSsoUserRepo.GetByIdAsync(postSqlSsoUser.Id);
            Assert.True(savedSqlSsoUser != null);

            // delete the tested object and assert its nonexistance
            await sqlSsoUserRepo.DeleteAsync(savedSqlSsoUser);
            savedSqlSsoUser = await sqlSsoUserRepo.GetByIdAsync(postSqlSsoUser.Id);
            Assert.True(savedSqlSsoUser == null);
        }

        [Theory, EfSsoUserAutoData]
        public async void DeleteAsync_UserIdOrganizationId_Works_DataMatches(TableModel.SsoUser ssoUser, SqlRepo.SsoUserRepository sqlSsoUserRepo, SsoUserCompare equalityComparer,
                SutProvider<EfRepo.SsoUserRepository> ssoUserRepoSut, SutProvider<EfRepo.UserRepository> userRepoSut, SutProvider<EfRepo.OrganizationRepository> orgRepoSut,
                TableModel.User user, TableModel.Organization org, SqlRepo.UserRepository sqlUserRepo, SqlRepo.OrganizationRepository sqlOrganizationRepo)
        {
            foreach(var option in DatabaseOptionsFactory.Options)
            {
                // satisfy any foreign key constraints for the tested object
                using (var context = new EfRepo.DatabaseContext(option))
                {
                    var savedEfUser = await userRepoSut.Sut.CreateAsync(context, user);
                    var savedEfOrg = await orgRepoSut.Sut.CreateAsync(context, org);
                    ssoUser.UserId = savedEfUser.Id;
                    ssoUser.OrganizationId = savedEfOrg.Id;
                }

                // save the tested object & assert its existance
                TableModel.SsoUser savedEfSsoUser = null;
                using (var context = new EfRepo.DatabaseContext(option))
                {
                    var postEfSsoUser = await ssoUserRepoSut.Sut.CreateAsync(context, ssoUser);
                    savedEfSsoUser = await ssoUserRepoSut.Sut.GetByIdAsync(context, postEfSsoUser.Id);
                    Assert.True(savedEfSsoUser != null);
                }

                // delete the tested object & assert its nonexistance
                using (var context = new EfRepo.DatabaseContext(option))
                {
                    await ssoUserRepoSut.Sut.DeleteAsync(context, savedEfSsoUser.UserId, savedEfSsoUser.OrganizationId);
                    savedEfSsoUser = await ssoUserRepoSut.Sut.GetByIdAsync(context, savedEfSsoUser.Id);
                    Assert.True(savedEfSsoUser == null);
                }
            }

            //init any objects that will be written to the database
            var sqlUser = await sqlUserRepo.CreateAsync(user);
            var sqlOrganization = await sqlOrganizationRepo.CreateAsync(org);
            ssoUser.UserId = sqlUser.Id;
            ssoUser.OrganizationId = sqlOrganization.Id;

            // save the tested object and assert its existance
            var postSqlSsoUser = await sqlSsoUserRepo.CreateAsync(ssoUser);
            var savedSqlSsoUser = await sqlSsoUserRepo.GetByIdAsync(postSqlSsoUser.Id);
            Assert.True(savedSqlSsoUser != null);

            // delete the tested object and assert its nonexistance
            await sqlSsoUserRepo.DeleteAsync(savedSqlSsoUser.UserId, savedSqlSsoUser.OrganizationId);
            savedSqlSsoUser = await sqlSsoUserRepo.GetByIdAsync(postSqlSsoUser.Id);
            Assert.True(savedSqlSsoUser == null);
        }
    }
}
