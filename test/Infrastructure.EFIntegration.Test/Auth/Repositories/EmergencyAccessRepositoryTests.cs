﻿using Bit.Core.Auth.Entities;
using Bit.Core.Entities;
using Bit.Core.Test.AutoFixture.Attributes;
using Bit.Infrastructure.EFIntegration.Test.Auth.AutoFixture;
using Bit.Infrastructure.EFIntegration.Test.Auth.Repositories.EqualityComparers;
using Xunit;
using EfAuthRepo = Bit.Infrastructure.EntityFramework.Auth.Repositories;
using EfRepo = Bit.Infrastructure.EntityFramework.Repositories;
using SqlAuthRepo = Bit.Infrastructure.Dapper.Auth.Repositories;
using SqlRepo = Bit.Infrastructure.Dapper.Repositories;

namespace Bit.Infrastructure.EFIntegration.Test.Auth.Repositories;

public class EmergencyAccessRepositoryTests
{
    [CiSkippedTheory, EfEmergencyAccessAutoData]
    public async Task CreateAsync_Works_DataMatches(
        EmergencyAccess emergencyAccess,
        List<User> users,
        EmergencyAccessCompare equalityComparer,
        List<EfAuthRepo.EmergencyAccessRepository> suts,
        List<EfRepo.UserRepository> efUserRepos,
        SqlAuthRepo.EmergencyAccessRepository sqlEmergencyAccessRepo,
        SqlRepo.UserRepository sqlUserRepo
    )
    {
        var savedEmergencyAccesses = new List<EmergencyAccess>();
        foreach (var sut in suts)
        {
            var i = suts.IndexOf(sut);

            for (int j = 0; j < users.Count; j++)
            {
                users[j] = await efUserRepos[i].CreateAsync(users[j]);
            }
            sut.ClearChangeTracking();

            emergencyAccess.GrantorId = users[0].Id;
            emergencyAccess.GranteeId = users[0].Id;
            var postEfEmergencyAccess = await sut.CreateAsync(emergencyAccess);
            sut.ClearChangeTracking();

            var savedEmergencyAccess = await sut.GetByIdAsync(postEfEmergencyAccess.Id);
            savedEmergencyAccesses.Add(savedEmergencyAccess);
        }

        for (int j = 0; j < users.Count; j++)
        {
            users[j] = await sqlUserRepo.CreateAsync(users[j]);
        }

        emergencyAccess.GrantorId = users[0].Id;
        emergencyAccess.GranteeId = users[0].Id;
        var sqlEmergencyAccess = await sqlEmergencyAccessRepo.CreateAsync(emergencyAccess);
        var savedSqlEmergencyAccess = await sqlEmergencyAccessRepo.GetByIdAsync(
            sqlEmergencyAccess.Id
        );
        savedEmergencyAccesses.Add(savedSqlEmergencyAccess);

        var distinctItems = savedEmergencyAccesses.Distinct(equalityComparer);
        Assert.True(!distinctItems.Skip(1).Any());
    }
}
