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
using System;

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

        [Theory, EfUserAutoData]
        public async void SearchAsync_Works_DataMatches(TableModel.User user, SqlRepo.UserRepository sqlUserRepo, 
                int skip, int take, UserCompare equalityCompare, SutProvider<EfRepo.UserRepository> sutProvider)
        {
            var searchedEfUsers = new List<TableModel.User>();
            foreach (var option in DatabaseOptionsFactory.Options)
            {
                using (var context = new EfRepo.DatabaseContext(option)) 
                {
                    var postEfUser = await sutProvider.Sut.CreateAsync(context, user);
                    var searchedEfUsersCollection = await sutProvider.Sut.SearchAsync(context, postEfUser.Email, skip, take);
                    searchedEfUsers.Concat(searchedEfUsersCollection.ToList());
                }
            }

            var postSqlUser = await sqlUserRepo.CreateAsync(user);
            var searchedSqlUsers = await sqlUserRepo.SearchAsync(postSqlUser.Email, skip, take);

            var distinctItems = searchedEfUsers.Concat(searchedSqlUsers).Distinct(equalityCompare);
            Assert.True(!distinctItems.Skip(1).Any());
        }

        [Theory, EfUserAutoData]
        public async void GetManyByPremiumAsync_Works_DataMatches(TableModel.User user, SqlRepo.UserRepository sqlUserRepo,
                SutProvider<EfRepo.UserRepository> sutProvider)
        {
            var returnedUsers = new List<TableModel.User>();
            foreach (var option in DatabaseOptionsFactory.Options)
            {
                using (var context = new EfRepo.DatabaseContext(option))
                {
                    var postEfUser = await sutProvider.Sut.CreateAsync(context, user);
                    var searchedEfUsers = await sutProvider.Sut.GetManyByPremiumAsync(context, user.Premium);
                    returnedUsers.Concat(searchedEfUsers.ToList());
                }
            }

            var postSqlUser = await sqlUserRepo.CreateAsync(user);
            var searchedSqlUsers = await sqlUserRepo.GetManyByPremiumAsync(user.Premium);
            returnedUsers.Concat(searchedSqlUsers.ToList());

            Assert.True(returnedUsers.All(x => x.Premium == user.Premium));
        }

        [Theory, EfUserAutoData]
        public async void GetPublicKeyAsync_Works_DataMatches(TableModel.User user, SqlRepo.UserRepository sqlUserRepo,
                SutProvider<EfRepo.UserRepository> sutProvider)
        {
            var returnedKeys = new List<string>();
            foreach (var option in DatabaseOptionsFactory.Options)
            {
                using (var context = new EfRepo.DatabaseContext(option))
                {
                    var postEfUser = await sutProvider.Sut.CreateAsync(context, user);
                    var efKey = await sutProvider.Sut.GetPublicKeyAsync(context, postEfUser.Id);
                    returnedKeys.Add(efKey);
                }
            }

            var postSqlUser = await sqlUserRepo.CreateAsync(user);
            var sqlKey = await sqlUserRepo.GetPublicKeyAsync(postSqlUser.Id);
            returnedKeys.Add(sqlKey);

            Assert.True(!returnedKeys.Distinct().Skip(1).Any());
        }

        [Theory, EfUserAutoData]
        public async void GetAccountRevisionDateAsync(TableModel.User user, SqlRepo.UserRepository sqlUserRepo,
                SutProvider<EfRepo.UserRepository> sutProvider)
        {
            var returnedKeys = new List<string>();
            foreach (var option in DatabaseOptionsFactory.Options)
            {
                using (var context = new EfRepo.DatabaseContext(option))
                {
                    var postEfUser = await sutProvider.Sut.CreateAsync(context, user);
                    var efKey = await sutProvider.Sut.GetPublicKeyAsync(context, postEfUser.Id);
                    returnedKeys.Add(efKey);
                }
            }

            var postSqlUser = await sqlUserRepo.CreateAsync(user);
            var sqlKey = await sqlUserRepo.GetPublicKeyAsync(postSqlUser.Id);
            returnedKeys.Add(sqlKey);

            Assert.True(!returnedKeys.Distinct().Skip(1).Any());
        }

        // TODO: need basic CipherRepo CRUD methods & fixtures to be built out and tested to properly test this method
        [Theory, EfUserAutoData]
        public void UpdateStorageAsync_Works_DataMatches(TableModel.User user, SqlRepo.UserRepository sqlUserRepo,
                List<TableModel.Cipher> ciphers, SutProvider<EfRepo.UserRepository> sutProvider)
        {
            Assert.True(true);
        }

        [Theory, EfUserAutoData]
        public async void UpdateRenewalReminderDateAsync_Works_DataMatches(TableModel.User user, SqlRepo.UserRepository sqlUserRepo,
                DateTime updatedReminderDate, SutProvider<EfRepo.UserRepository> sutProvider)
        {
            var savedDates = new List<DateTime?>();
            foreach(var option in DatabaseOptionsFactory.Options)
            {
                var postEfUser = user;
                using (var context = new EfRepo.DatabaseContext(option))
                {
                    postEfUser = await sutProvider.Sut.CreateAsync(context, user);
                }

                using (var context = new EfRepo.DatabaseContext(option))
                {
                    await sutProvider.Sut.UpdateRenewalReminderDateAsync(context, postEfUser.Id, updatedReminderDate);
                }

                using (var context = new EfRepo.DatabaseContext(option))
                {
                    var replacedUser = await sutProvider.Sut.GetByIdAsync(context, postEfUser.Id);
                    savedDates.Add(replacedUser.RenewalReminderDate);
                }
            }

            var postSqlUser = await sqlUserRepo.CreateAsync(user);
            await sqlUserRepo.UpdateRenewalReminderDateAsync(postSqlUser.Id, updatedReminderDate);
            var replacedSqlUser = await sqlUserRepo.GetByIdAsync(postSqlUser.Id);
            savedDates.Add(replacedSqlUser.RenewalReminderDate);

            var distinctItems = savedDates.GroupBy(e => e.ToString());
            Assert.True(!distinctItems.Skip(1).Any() && savedDates.All(e => e.ToString() == updatedReminderDate.ToString()));
        }

        // TODO: need basic SsoUserRepo CRUD methoids & fixtures to be built out and tested to properly test this method
        [Theory, EfUserAutoData]
        public void GetBySsoUserAsync_Works_DataMatches(TableModel.User user, SqlRepo.UserRepository sqlUserRepo,
                SutProvider<EfRepo.UserRepository> sutProvider)
        {
            Assert.True(true);
        }
    }
}
