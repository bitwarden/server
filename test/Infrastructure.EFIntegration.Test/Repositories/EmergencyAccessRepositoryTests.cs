using Bit.Core.Auth.Entities;
using Bit.Core.Entities;
using Bit.Core.Test.AutoFixture.Attributes;
using Bit.Infrastructure.EFIntegration.Test.AutoFixture;
using Bit.Infrastructure.EFIntegration.Test.Repositories.EqualityComparers;
using Xunit;
using EfRepo = Bit.Infrastructure.EntityFramework.Repositories;
using SqlRepo = Bit.Infrastructure.Dapper.Repositories;

namespace Bit.Infrastructure.EFIntegration.Test.Repositories;

public class EmergencyAccessRepositoryTests
{
    [CiSkippedTheory, EfEmergencyAccessAutoData]
    public async void CreateAsync_Works_DataMatches(
        EmergencyAccess emergencyAccess,
        List<User> users,
        EmergencyAccessCompare equalityComparer,
        List<EfRepo.EmergencyAccessRepository> suts,
        List<EfRepo.UserRepository> efUserRepos,
        SqlRepo.EmergencyAccessRepository sqlEmergencyAccessRepo,
        SqlRepo.UserRepository sqlUserRepo
        )
    {
        var savedEmergencyAccesss = new List<EmergencyAccess>();
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
            savedEmergencyAccesss.Add(savedEmergencyAccess);
        }

        for (int j = 0; j < users.Count; j++)
        {
            users[j] = await sqlUserRepo.CreateAsync(users[j]);
        }

        emergencyAccess.GrantorId = users[0].Id;
        emergencyAccess.GranteeId = users[0].Id;
        var sqlEmergencyAccess = await sqlEmergencyAccessRepo.CreateAsync(emergencyAccess);
        var savedSqlEmergencyAccess = await sqlEmergencyAccessRepo.GetByIdAsync(sqlEmergencyAccess.Id);
        savedEmergencyAccesss.Add(savedSqlEmergencyAccess);

        var distinctItems = savedEmergencyAccesss.Distinct(equalityComparer);
        Assert.True(!distinctItems.Skip(1).Any());
    }
}
