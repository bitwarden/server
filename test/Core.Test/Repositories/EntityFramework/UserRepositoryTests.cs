using AutoMapper;
using Bit.Core.Test.AutoFixture.UserFixtures;
using Bit.Core.Test.AutoFixture;
using Bit.Core.Test.Helpers.Factories;
using EfRepo = Bit.Core.Repositories.EntityFramework;
using SqlRepo = Bit.Core.Repositories.SqlServer;
using System.Collections.Generic;
using System.Linq;
using TableModel = Bit.Core.Models.Table;
using Xunit;
using Bit.Core.Test.Repositories.EntityFramework.EqualityComparers;
using Bit.Core.Models.Data;

namespace Bit.Core.Test.Repositories.EntityFramework
{
    public class UserRepositoryTests
    {
        [Theory, EfUserAutoData]
        public async void CreateAsync_Works_DataMatches(TableModel.User user, SqlRepo.UserRepository sqlUserRepo,
                IMapper mapper, UserCompare equalityComparer, SutProvider<EfRepo.UserRepository> sutProvider)
        {
            var savedUsers = new List<TableModel.User>();
            foreach(var option in DatabaseOptionsFactory.Options)
            {
                using (var context = new EfRepo.DatabaseContext(option))
                {
                    var postEfUser = await sutProvider.Sut.CreateAsync(context, user);
                    var savedUser = await sutProvider.Sut.GetByIdAsync(context, postEfUser.Id);
                    savedUsers.Add(savedUser);
                }
            }

            var sqlUser = await sqlUserRepo.CreateAsync(user);
            savedUsers.Add(await sqlUserRepo.GetByIdAsync(sqlUser.Id));

            var distinctItems = savedUsers.Distinct(equalityComparer);
            Assert.True(!distinctItems.Skip(1).Any());
        }

        [Theory, EfUserAutoData]
        public async void ReplaceAsync_Works_DataMatches(TableModel.User postUser, TableModel.User replaceUser, 
                SqlRepo.UserRepository sqlUserRepo, UserCompare equalityComparer, SutProvider<EfRepo.UserRepository> sutProvider)
        {
            var savedUsers = new List<TableModel.User>();
            foreach(var option in DatabaseOptionsFactory.Options)
            {
                using (var context = new EfRepo.DatabaseContext(option))
                {
                    var postEfUser = await sutProvider.Sut.CreateAsync(context, postUser);
                    replaceUser.Id = postEfUser.Id;
                    await sutProvider.Sut.ReplaceAsync(context, replaceUser);
                    var replacedUser = await sutProvider.Sut.GetByIdAsync(context, replaceUser.Id);
                    savedUsers.Add(replacedUser);
                }
            }

            var postSqlUser = await sqlUserRepo.CreateAsync(postUser);
            replaceUser.Id = postSqlUser.Id;
            await sqlUserRepo.ReplaceAsync(replaceUser);
            savedUsers.Add(await sqlUserRepo.GetByIdAsync(replaceUser.Id));

            var distinctItems = savedUsers.Distinct(equalityComparer);
            Assert.True(!distinctItems.Skip(1).Any());
        }

        [Theory, EfUserAutoData]
        public async void DeleteAsync_Works_DataMatches(TableModel.User user, SqlRepo.UserRepository sqlUserRepo, UserCompare equalityComparer,
                SutProvider<EfRepo.UserRepository> sutProvider)
        {
            foreach(var option in DatabaseOptionsFactory.Options)
            {
                TableModel.User savedEfUser = null;
                using (var context = new EfRepo.DatabaseContext(option))
                {
                    var postEfUser = await sutProvider.Sut.CreateAsync(context, user);
                    savedEfUser = await sutProvider.Sut.GetByIdAsync(context, postEfUser.Id);
                    Assert.True(savedEfUser != null);
                }

                using (var context = new EfRepo.DatabaseContext(option))
                {
                    await sutProvider.Sut.DeleteAsync(context, savedEfUser);
                    savedEfUser = await sutProvider.Sut.GetByIdAsync(context, savedEfUser.Id);
                    Assert.True(savedEfUser == null);
                }
            }

            var postSqlUser = await sqlUserRepo.CreateAsync(user);
            var savedSqlUser = await sqlUserRepo.GetByIdAsync(postSqlUser.Id);
            Assert.True(savedSqlUser != null);

            await sqlUserRepo.DeleteAsync(postSqlUser);
            savedSqlUser = await sqlUserRepo.GetByIdAsync(postSqlUser.Id);
            Assert.True(savedSqlUser == null);
        }

        [Theory, EfUserAutoData]
        public async void GetByEmailAsync_Works_DataMatches(TableModel.User user, SqlRepo.UserRepository sqlUserRepo,
                UserCompare equalityComparer, SutProvider<EfRepo.UserRepository> sutProvider)
        {
            var savedUsers = new List<TableModel.User>();
            foreach(var option in DatabaseOptionsFactory.Options)
            {
                using (var context = new EfRepo.DatabaseContext(option))
                {
                    var postEfUser = await sutProvider.Sut.CreateAsync(context, user);
                    var savedUser = await sutProvider.Sut.GetByEmailAsync(context, postEfUser.Email);
                    savedUsers.Add(savedUser);
                }
            }

            var postSqlUser = await sqlUserRepo.CreateAsync(user);
            savedUsers.Add(await sqlUserRepo.GetByEmailAsync(postSqlUser.Email));

            var distinctItems = savedUsers.Distinct(equalityComparer);
            Assert.True(!distinctItems.Skip(1).Any());
        }

        [Theory, EfUserAutoData]
        public async void GetKdfInformationByEmailAsync_Works_DataMatches(TableModel.User user, SqlRepo.UserRepository sqlUserRepo,
                UserKdfInformationCompare equalityComparer, SutProvider<EfRepo.UserRepository> sutProvider)
        {
            
            var savedKdfInformation = new List<UserKdfInformation>();
            foreach(var option in DatabaseOptionsFactory.Options)
            {
                using (var context = new EfRepo.DatabaseContext(option))
                {
                    var postEfUser = await sutProvider.Sut.CreateAsync(context, user);
                    var kdfInformation = await sutProvider.Sut.GetKdfInformationByEmailAsync(context, postEfUser.Email);
                    savedKdfInformation.Add(kdfInformation);
                }
            }

            var postSqlUser = await sqlUserRepo.CreateAsync(user);
            var sqlKdfInformation = await sqlUserRepo.GetKdfInformationByEmailAsync(postSqlUser.Email);
            savedKdfInformation.Add(sqlKdfInformation);

            var distinctItems = savedKdfInformation.Distinct(equalityComparer);
            Assert.True(!distinctItems.Skip(1).Any());
        }
    }
}
