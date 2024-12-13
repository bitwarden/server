using Bit.Core.Auth.Entities;
using Bit.Core.Auth.Enums;
using Bit.Core.Auth.Models.Api.Response;
using Bit.Core.Entities;
using Bit.Core.Test.AutoFixture.Attributes;
using Bit.Infrastructure.EFIntegration.Test.AutoFixture;
using Bit.Infrastructure.EFIntegration.Test.Repositories.EqualityComparers;
using Xunit;
using EfAuthRepo = Bit.Infrastructure.EntityFramework.Auth.Repositories;
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
    [CiSkippedTheory, EfDeviceAutoData]
    public async Task GetManyByUserIdWithDeviceAuth_Works_ReturnsExpectedResults(
        Device device,
        User user,
        AuthRequest authRequest,
        List<EfRepo.DeviceRepository> efSuts,
        List<EfRepo.UserRepository> efUserRepos,
        List<EfAuthRepo.AuthRequestRepository> efAuthRequestRepos,
        SqlRepo.DeviceRepository sqlSut,
        SqlRepo.UserRepository sqlUserRepo,
        SqlAuthRepo.AuthRequestRepository sqlAuthRequestRepository
        )
    {
        // Arrange
        var allResponses = new List<ICollection<DeviceAuthRequestResponseModel>>();

        device.Active = true;

        authRequest.ResponseDeviceId = null;
        authRequest.Type = AuthRequestType.Unlock;
        authRequest.CreationDate = DateTime.Now;
        authRequest.Approved = null;
        authRequest.OrganizationId = null;

        // Entity Framework Repo
        foreach (var efSut in efSuts)
        {
            var i = efSuts.IndexOf(efSut);

            // Create user
            var efUser = await efUserRepos[i].CreateAsync(user);
            efSut.ClearChangeTracking();

            // Create device
            device.UserId = efUser.Id;
            device.Name = "test-ef-chrome";

            var efDevice = await efSuts[i].CreateAsync(device);
            efSut.ClearChangeTracking();

            // Create auth request
            authRequest.UserId = efUser.Id;
            authRequest.RequestDeviceIdentifier = efDevice.Identifier;

            await efAuthRequestRepos[i].CreateAsync(authRequest);
        }

        // Dapper Repo
        // Create user
        var sqlUser = await sqlUserRepo.CreateAsync(user);

        // Create device
        device.UserId = sqlUser.Id;
        device.Name = "test-sql-chrome";
        var sqlDevice = await sqlSut.CreateAsync(device);

        // Create auth request
        authRequest.UserId = sqlUser.Id;
        authRequest.RequestDeviceIdentifier = sqlDevice.Identifier;
        await sqlAuthRequestRepository.CreateAsync(authRequest);

        // Act

        // Sql Responses
        allResponses.Add(await sqlSut.GetManyByUserIdWithDeviceAuth(user.Id, 15));

        // Entity Framework Responses
        foreach (var efSut in efSuts)
        {
            allResponses.Add(await efSut.GetManyByUserIdWithDeviceAuth(user.Id, 15));
        }

        // Assert
        var totalExpectedSuccessfulQueries = efSuts.Count + 1;
        Assert.True(allResponses.Count == totalExpectedSuccessfulQueries);
    }
}
