using Bit.Core.Auth.Entities;
using Bit.Core.Auth.Enums;
using Bit.Core.Entities;
using Bit.Core.Test.AutoFixture.Attributes;
using Bit.Infrastructure.EFIntegration.Test.AutoFixture;
using Bit.Infrastructure.EFIntegration.Test.Repositories.EqualityComparers;
using Xunit;
using EfRepo = Bit.Infrastructure.EntityFramework.Repositories;
using SqlAuthRepo = Bit.Infrastructure.Dapper.Auth.Repositories;
using SqlRepo = Bit.Infrastructure.Dapper.Repositories;

namespace Bit.Infrastructure.EFIntegration.Test.Repositories;

public class DeviceRepositoryTests
{
    [CiSkippedTheory, EfDeviceAutoData]
    public async Task CreateAsync_Works_DataMatches(
        Device device,
        User user,
        DeviceCompare equalityComparer,
        List<EfRepo.DeviceRepository> suts,
        List<EfRepo.UserRepository> efUserRepos,
        SqlRepo.DeviceRepository sqlDeviceRepo,
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
        Assert.False(distinctItems.Skip(1).Any());
    }

    // Test Cases:

    // Just works
    [CiSkippedTheory, EfDeviceAutoData]
    public async Task GetManyByUserIdWithDeviceAuth_Works_ReturnsExpectedResults(
        Device device,
        User user,
        AuthRequest authRequest,
        // List<EfRepo.DeviceRepository> suts,
        // List<EfRepo.UserRepository> efUserRepos,
        // List<EfAuthRepo.AuthRequestRepository> efAuthRequestRepos,
        SqlRepo.DeviceRepository sqlDeviceRepo,
        SqlRepo.UserRepository sqlUserRepo,
        SqlAuthRepo.AuthRequestRepository sqlAuthRequestRepository
        )
    {
        // Arrange
        // Create user
        var sqlUser = await sqlUserRepo.CreateAsync(user);

        // Create device
        device.UserId = user.Id;
        device.Active = true;
        device.Name = "chrome";
        var sqlDevice = await sqlDeviceRepo.CreateAsync(device);

        // Create auth request
        authRequest.UserId = user.Id;
        authRequest.RequestDeviceIdentifier = device.Identifier;
        authRequest.ResponseDeviceId = null;
        authRequest.Type = AuthRequestType.Unlock;
        authRequest.CreationDate = DateTime.UtcNow;
        authRequest.Approved = null;
        authRequest.OrganizationId = null;
        var sqlAuthRequest = await sqlAuthRequestRepository.CreateAsync(authRequest);

        // Act
        var response = await sqlDeviceRepo.GetManyByUserIdWithDeviceAuth(user.Id, 15);

        // Assert
        Assert.True(response.Count == 1);

        // var savedDevices = new List<Device>();
        // foreach (var sut in suts)
        // {
        //     var i = suts.IndexOf(sut);
        //
        //     var efUser = await efUserRepos[i].CreateAsync(user);
        //     device.UserId = efUser.Id;
        //     sut.ClearChangeTracking();
        //
        //     var postEfDevice = await sut.CreateAsync(device);
        //     sut.ClearChangeTracking();
        //
        //     var savedDevice = await sut.GetByIdAsync(postEfDevice.Id);
        //     savedDevices.Add(savedDevice);
        //
        //     var efAuthRequest = await efAuthRequestRepos[i].CreateAsync(authRequest);
        //     authRequest.Id = postEfDevice.Id;
        // }
        //
        // var sqlUser = await sqlUserRepo.CreateAsync(user);
        // device.UserId = sqlUser.Id;
        //
        // var sqlDevice = await sqlDeviceRepo.CreateAsync(device);
        // var savedSqlDevice = await sqlDeviceRepo.GetByIdAsync(sqlDevice.Id);
        // savedDevices.Add(savedSqlDevice);
        //
        // var sqlAuthRequest = await sqlAuthRequestRepository.CreateAsync(authRequest);
        // authRequest.Id = sqlDevice.Id;
        //
        // var devicesWithAuth = await sqlDeviceRepo.GetManyByUserIdWithDeviceAuth(user.Id, 30);
        // Assert.NotEmpty(devicesWithAuth);
        // Assert.All(devicesWithAuth, d => Assert.Equal(user.Id, d.Id));
    }
    // Most recent auth request is provided.

    // Change the conditions using [InlineData]
}
