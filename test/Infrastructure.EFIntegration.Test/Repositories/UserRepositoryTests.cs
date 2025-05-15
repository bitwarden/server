﻿using Bit.Core.AdminConsole.Entities;
using Bit.Core.Auth.Entities;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Models.Data;
using Bit.Core.Test.AutoFixture.Attributes;
using Bit.Infrastructure.EFIntegration.Test.AutoFixture;
using Bit.Infrastructure.EFIntegration.Test.Repositories.EqualityComparers;
using Xunit;
using EfRepo = Bit.Infrastructure.EntityFramework.Repositories;
using SqlAuthRepo = Bit.Infrastructure.Dapper.Auth.Repositories;
using SqlRepo = Bit.Infrastructure.Dapper.Repositories;

namespace Bit.Infrastructure.EFIntegration.Test.Repositories;

public class UserRepositoryTests
{
    [CiSkippedTheory, EfUserAutoData]
    public async Task CreateAsync_Works_DataMatches(
        User user, UserCompare equalityComparer,
        List<EfRepo.UserRepository> suts,
        SqlRepo.UserRepository sqlUserRepo
        )
    {
        var savedUsers = new List<User>();

        foreach (var sut in suts)
        {
            var postEfUser = await sut.CreateAsync(user);

            sut.ClearChangeTracking();

            var savedUser = await sut.GetByIdAsync(postEfUser.Id);
            savedUsers.Add(savedUser);
        }

        var sqlUser = await sqlUserRepo.CreateAsync(user);
        savedUsers.Add(await sqlUserRepo.GetByIdAsync(sqlUser.Id));

        var distinctItems = savedUsers.Distinct(equalityComparer);
        Assert.True(!distinctItems.Skip(1).Any());
    }

    [CiSkippedTheory, EfUserAutoData]
    public async Task ReplaceAsync_Works_DataMatches(User postUser, User replaceUser,
        UserCompare equalityComparer, List<EfRepo.UserRepository> suts,
        SqlRepo.UserRepository sqlUserRepo)
    {
        var savedUsers = new List<User>();
        foreach (var sut in suts)
        {
            var postEfUser = await sut.CreateAsync(postUser);
            replaceUser.Id = postEfUser.Id;
            await sut.ReplaceAsync(replaceUser);
            var replacedUser = await sut.GetByIdAsync(replaceUser.Id);
            savedUsers.Add(replacedUser);
        }

        var postSqlUser = await sqlUserRepo.CreateAsync(postUser);
        replaceUser.Id = postSqlUser.Id;
        await sqlUserRepo.ReplaceAsync(replaceUser);
        savedUsers.Add(await sqlUserRepo.GetByIdAsync(replaceUser.Id));

        var distinctItems = savedUsers.Distinct(equalityComparer);
        Assert.True(!distinctItems.Skip(1).Any());
    }

    [CiSkippedTheory, EfUserAutoData]
    public async Task DeleteAsync_Works_DataMatches(User user, List<EfRepo.UserRepository> suts, SqlRepo.UserRepository sqlUserRepo)
    {
        foreach (var sut in suts)
        {
            var postEfUser = await sut.CreateAsync(user);
            sut.ClearChangeTracking();

            var savedEfUser = await sut.GetByIdAsync(postEfUser.Id);
            Assert.True(savedEfUser != null);
            sut.ClearChangeTracking();

            await sut.DeleteAsync(savedEfUser);
            sut.ClearChangeTracking();

            savedEfUser = await sut.GetByIdAsync(savedEfUser.Id);
            Assert.True(savedEfUser == null);
        }

        var postSqlUser = await sqlUserRepo.CreateAsync(user);
        var savedSqlUser = await sqlUserRepo.GetByIdAsync(postSqlUser.Id);
        Assert.True(savedSqlUser != null);

        await sqlUserRepo.DeleteAsync(postSqlUser);
        savedSqlUser = await sqlUserRepo.GetByIdAsync(postSqlUser.Id);
        Assert.True(savedSqlUser == null);
    }

    [CiSkippedTheory, EfUserAutoData]
    public async Task GetByEmailAsync_Works_DataMatches(User user, UserCompare equalityComparer,
            List<EfRepo.UserRepository> suts, SqlRepo.UserRepository sqlUserRepo)
    {
        var savedUsers = new List<User>();
        foreach (var sut in suts)
        {
            var postEfUser = await sut.CreateAsync(user);
            sut.ClearChangeTracking();
            var savedUser = await sut.GetByEmailAsync(postEfUser.Email.ToUpperInvariant());
            savedUsers.Add(savedUser);
        }

        var postSqlUser = await sqlUserRepo.CreateAsync(user);
        savedUsers.Add(await sqlUserRepo.GetByEmailAsync(postSqlUser.Email.ToUpperInvariant()));

        var distinctItems = savedUsers.Distinct(equalityComparer);
        Assert.True(!distinctItems.Skip(1).Any());
    }

    [CiSkippedTheory, EfUserAutoData]
    public async Task GetKdfInformationByEmailAsync_Works_DataMatches(User user,
        UserKdfInformationCompare equalityComparer, List<EfRepo.UserRepository> suts,
        SqlRepo.UserRepository sqlUserRepo)
    {
        var savedKdfInformation = new List<UserKdfInformation>();
        foreach (var sut in suts)
        {
            var postEfUser = await sut.CreateAsync(user);
            sut.ClearChangeTracking();
            var kdfInformation = await sut.GetKdfInformationByEmailAsync(postEfUser.Email.ToUpperInvariant());
            savedKdfInformation.Add(kdfInformation);
        }

        var postSqlUser = await sqlUserRepo.CreateAsync(user);
        var sqlKdfInformation = await sqlUserRepo.GetKdfInformationByEmailAsync(postSqlUser.Email);
        savedKdfInformation.Add(sqlKdfInformation);

        var distinctItems = savedKdfInformation.Distinct(equalityComparer);
        Assert.True(!distinctItems.Skip(1).Any());
    }

    [CiSkippedTheory, EfUserAutoData]
    public async Task SearchAsync_Works_DataMatches(User user, int skip, int take,
        UserCompare equalityCompare, List<EfRepo.UserRepository> suts,
        SqlRepo.UserRepository sqlUserRepo)
    {
        var searchedEfUsers = new List<User>();
        foreach (var sut in suts)
        {
            var postEfUser = await sut.CreateAsync(user);
            sut.ClearChangeTracking();

            var searchedEfUsersCollection = await sut.SearchAsync(postEfUser.Email.ToUpperInvariant(), skip, take);
            searchedEfUsers.Concat(searchedEfUsersCollection.ToList());
        }

        var postSqlUser = await sqlUserRepo.CreateAsync(user);
        var searchedSqlUsers = await sqlUserRepo.SearchAsync(postSqlUser.Email.ToUpperInvariant(), skip, take);

        var distinctItems = searchedEfUsers.Concat(searchedSqlUsers).Distinct(equalityCompare);
        Assert.True(!distinctItems.Skip(1).Any());
    }

    [CiSkippedTheory, EfUserAutoData]
    public async Task GetManyByPremiumAsync_Works_DataMatches(User user,
        List<EfRepo.UserRepository> suts, SqlRepo.UserRepository sqlUserRepo)
    {
        var returnedUsers = new List<User>();
        foreach (var sut in suts)
        {
            var postEfUser = await sut.CreateAsync(user);
            sut.ClearChangeTracking();

            var searchedEfUsers = await sut.GetManyByPremiumAsync(user.Premium);
            returnedUsers.Concat(searchedEfUsers.ToList());
        }

        var postSqlUser = await sqlUserRepo.CreateAsync(user);
        var searchedSqlUsers = await sqlUserRepo.GetManyByPremiumAsync(user.Premium);
        returnedUsers.Concat(searchedSqlUsers.ToList());

        Assert.True(returnedUsers.All(x => x.Premium == user.Premium));
    }

    [CiSkippedTheory, EfUserAutoData]
    public async Task GetPublicKeyAsync_Works_DataMatches(User user, List<EfRepo.UserRepository> suts,
        SqlRepo.UserRepository sqlUserRepo)
    {
        var returnedKeys = new List<string>();
        foreach (var sut in suts)
        {
            var postEfUser = await sut.CreateAsync(user);
            sut.ClearChangeTracking();

            var efKey = await sut.GetPublicKeyAsync(postEfUser.Id);
            returnedKeys.Add(efKey);
        }

        var postSqlUser = await sqlUserRepo.CreateAsync(user);
        var sqlKey = await sqlUserRepo.GetPublicKeyAsync(postSqlUser.Id);
        returnedKeys.Add(sqlKey);

        Assert.True(!returnedKeys.Distinct().Skip(1).Any());
    }

    [CiSkippedTheory, EfUserAutoData]
    public async Task GetAccountRevisionDateAsync(User user, List<EfRepo.UserRepository> suts,
        SqlRepo.UserRepository sqlUserRepo)
    {
        var returnedKeys = new List<string>();
        foreach (var sut in suts)
        {
            var postEfUser = await sut.CreateAsync(user);
            sut.ClearChangeTracking();

            var efKey = await sut.GetPublicKeyAsync(postEfUser.Id);
            returnedKeys.Add(efKey);
        }

        var postSqlUser = await sqlUserRepo.CreateAsync(user);
        var sqlKey = await sqlUserRepo.GetPublicKeyAsync(postSqlUser.Id);
        returnedKeys.Add(sqlKey);

        Assert.True(!returnedKeys.Distinct().Skip(1).Any());
    }

    [CiSkippedTheory, EfUserAutoData]
    public async Task UpdateRenewalReminderDateAsync_Works_DataMatches(User user,
        DateTime updatedReminderDate, List<EfRepo.UserRepository> suts,
        SqlRepo.UserRepository sqlUserRepo)
    {
        var savedDates = new List<DateTime?>();
        foreach (var sut in suts)
        {
            var postEfUser = user;
            postEfUser = await sut.CreateAsync(user);
            sut.ClearChangeTracking();

            await sut.UpdateRenewalReminderDateAsync(postEfUser.Id, updatedReminderDate);
            sut.ClearChangeTracking();

            var replacedUser = await sut.GetByIdAsync(postEfUser.Id);
            savedDates.Add(replacedUser.RenewalReminderDate);
        }

        var postSqlUser = await sqlUserRepo.CreateAsync(user);
        await sqlUserRepo.UpdateRenewalReminderDateAsync(postSqlUser.Id, updatedReminderDate);
        var replacedSqlUser = await sqlUserRepo.GetByIdAsync(postSqlUser.Id);
        savedDates.Add(replacedSqlUser.RenewalReminderDate);

        var distinctItems = savedDates.GroupBy(e => e.ToString());
        Assert.True(!distinctItems.Skip(1).Any() &&
                savedDates.All(e => e.ToString() == updatedReminderDate.ToString()));
    }

    [CiSkippedTheory, EfUserAutoData]
    public async Task GetBySsoUserAsync_Works_DataMatches(User user, Organization org,
        SsoUser ssoUser, UserCompare equalityComparer, List<EfRepo.UserRepository> suts,
        List<EfRepo.SsoUserRepository> ssoUserRepos, List<EfRepo.OrganizationRepository> orgRepos,
        SqlRepo.UserRepository sqlUserRepo, SqlAuthRepo.SsoUserRepository sqlSsoUserRepo,
        SqlRepo.OrganizationRepository sqlOrgRepo)
    {
        var returnedList = new List<User>();
        foreach (var sut in suts)
        {
            var i = suts.IndexOf(sut);

            var postEfUser = await sut.CreateAsync(user);
            sut.ClearChangeTracking();

            var efOrg = await orgRepos[i].CreateAsync(org);
            sut.ClearChangeTracking();

            ssoUser.UserId = postEfUser.Id;
            ssoUser.OrganizationId = efOrg.Id;
            var postEfSsoUser = await ssoUserRepos[i].CreateAsync(ssoUser);
            sut.ClearChangeTracking();

            var returnedUser = await sut.GetBySsoUserAsync(postEfSsoUser.ExternalId.ToUpperInvariant(), efOrg.Id);
            returnedList.Add(returnedUser);
        }

        var sqlUser = await sqlUserRepo.CreateAsync(user);
        var sqlOrganization = await sqlOrgRepo.CreateAsync(org);

        ssoUser.UserId = sqlUser.Id;
        ssoUser.OrganizationId = sqlOrganization.Id;
        var postSqlSsoUser = await sqlSsoUserRepo.CreateAsync(ssoUser);

        var returnedSqlUser = await sqlUserRepo
            .GetBySsoUserAsync(postSqlSsoUser.ExternalId, sqlOrganization.Id);
        returnedList.Add(returnedSqlUser);

        var distinctItems = returnedList.Distinct(equalityComparer);
        Assert.True(!distinctItems.Skip(1).Any());
    }

    [CiSkippedTheory, EfUserAutoData]
    public async Task UpdateUserKeyAndEncryptedDataAsync_Works_DataMatches(User user, SqlRepo.UserRepository sqlUserRepo)
    {
        var sqlUser = await sqlUserRepo.CreateAsync(user);
        sqlUser.Kdf = KdfType.PBKDF2_SHA256;
        sqlUser.KdfIterations = 6_000_000;
        sqlUser.KdfMemory = 7_000_000;
        sqlUser.KdfParallelism = 8_000_000;
        sqlUser.MasterPassword = "masterPasswordHash";
        sqlUser.MasterPasswordHint = "masterPasswordHint";
        sqlUser.Email = "example@example.com";

        await sqlUserRepo.UpdateUserKeyAndEncryptedDataV2Async(sqlUser, []);
        var updatedUser = await sqlUserRepo.GetByIdAsync(sqlUser.Id);
        Assert.Equal(sqlUser.Kdf, updatedUser.Kdf);
        Assert.Equal(sqlUser.KdfIterations, updatedUser.KdfIterations);
        Assert.Equal(sqlUser.KdfMemory, updatedUser.KdfMemory);
        Assert.Equal(sqlUser.KdfParallelism, updatedUser.KdfParallelism);
        Assert.Equal(sqlUser.MasterPassword, updatedUser.MasterPassword);
        Assert.Equal(sqlUser.MasterPasswordHint, updatedUser.MasterPasswordHint);
        Assert.Equal(sqlUser.Email, updatedUser.Email);
    }
}
