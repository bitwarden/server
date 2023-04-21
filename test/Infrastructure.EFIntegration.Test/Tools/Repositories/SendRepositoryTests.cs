using Bit.Core.Entities;
using Bit.Core.Test.AutoFixture.Attributes;
using Bit.Core.Tools.Entities;
using Bit.Infrastructure.EFIntegration.Test.Tools.AutoFixture;
using Bit.Infrastructure.EFIntegration.Test.Tools.Repositories.EqualityComparers;
using Xunit;
using EfRepo = Bit.Infrastructure.EntityFramework.Repositories;
using EfSendRepo = Bit.Infrastructure.EntityFramework.Tools.Repositories;
using SqlRepo = Bit.Infrastructure.Dapper.Repositories;
using SqlSendRepo = Bit.Infrastructure.Dapper.Tools.Repositories;

namespace Bit.Infrastructure.EFIntegration.Test.Tools.Repositories;

public class SendRepositoryTests
{
    [CiSkippedTheory, EfUserSendAutoData, EfOrganizationSendAutoData]
    public async void CreateAsync_Works_DataMatches(
        Send send,
        User user,
        Organization org,
        SendCompare equalityComparer,
        List<EfSendRepo.SendRepository> suts,
        List<EfRepo.UserRepository> efUserRepos,
        List<EfRepo.OrganizationRepository> efOrgRepos,
        SqlSendRepo.SendRepository sqlSendRepo,
        SqlRepo.UserRepository sqlUserRepo,
        SqlRepo.OrganizationRepository sqlOrgRepo
        )
    {
        var savedSends = new List<Send>();
        foreach (var sut in suts)
        {
            var i = suts.IndexOf(sut);

            if (send.OrganizationId.HasValue)
            {
                var efOrg = await efOrgRepos[i].CreateAsync(org);
                sut.ClearChangeTracking();
                send.OrganizationId = efOrg.Id;
            }
            var efUser = await efUserRepos[i].CreateAsync(user);
            sut.ClearChangeTracking();

            send.UserId = efUser.Id;
            var postEfSend = await sut.CreateAsync(send);
            sut.ClearChangeTracking();

            var savedSend = await sut.GetByIdAsync(postEfSend.Id);
            savedSends.Add(savedSend);
        }

        var sqlUser = await sqlUserRepo.CreateAsync(user);
        if (send.OrganizationId.HasValue)
        {
            var sqlOrg = await sqlOrgRepo.CreateAsync(org);
            send.OrganizationId = sqlOrg.Id;
        }

        send.UserId = sqlUser.Id;
        var sqlSend = await sqlSendRepo.CreateAsync(send);
        var savedSqlSend = await sqlSendRepo.GetByIdAsync(sqlSend.Id);
        savedSends.Add(savedSqlSend);

        var distinctItems = savedSends.Distinct(equalityComparer);
        Assert.True(!distinctItems.Skip(1).Any());
    }
}
