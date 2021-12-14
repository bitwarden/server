using System.Collections.Generic;
using System.Linq;
using Bit.Core.Models.Table;
using Bit.Core.Test.AutoFixture.Attributes;
using Bit.Core.Test.AutoFixture.DeviceFixtures;
using Bit.Core.Test.Repositories.EntityFramework.EqualityComparers;
using Xunit;
using EfRepo = Bit.Core.Repositories.EntityFramework;
using SqlRepo = Bit.Core.Repositories.SqlServer;

namespace Bit.Core.Test.Repositories.EntityFramework
{
    public class DeviceRepositoryTests
    {
        [CiSkippedTheory, EfDeviceAutoData]
        public async void CreateAsync_Works_DataMatches(Device device, User user,
            DeviceCompare equalityComparer, List<EfRepo.DeviceRepository> suts,
            List<EfRepo.UserRepository> efUserRepos, SqlRepo.DeviceRepository sqlDeviceRepo,
            SqlRepo.UserRepository sqlUserRepo)
        {
            var savedDevices = new List<Device>();
            foreach (var sut in suts)
            {
                var i = suts.IndexOf(sut);

                var efUser = await efUserRepos[i].CreateAsync(user);
                device.UserId = efUser.Id;
                sut.ClearChangeTracking();

                var postEfDevice = await sut.CreateAsync(device);
                sut.ClearChangeTracking();

                var savedDevice = await sut.GetByIdAsync(postEfDevice.Id);
                savedDevices.Add(savedDevice);
            }

            var sqlUser = await sqlUserRepo.CreateAsync(user);
            device.UserId = sqlUser.Id;

            var sqlDevice = await sqlDeviceRepo.CreateAsync(device);
            var savedSqlDevice = await sqlDeviceRepo.GetByIdAsync(sqlDevice.Id);
            savedDevices.Add(savedSqlDevice);

            var distinctItems = savedDevices.Distinct(equalityComparer);
            Assert.True(!distinctItems.Skip(1).Any());
        }

    }
}
