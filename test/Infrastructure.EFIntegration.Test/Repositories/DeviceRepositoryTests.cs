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
        var userIdsToSearchOn = new List<Guid>();
        var expirationTime = 15;
        var correctAuthRequestsToRetrieve = new List<Guid>();

        // Configure data for successful responses.
        device.Active = true;

        authRequest.ResponseDeviceId = null;
        authRequest.Approved = null;
        authRequest.Type = AuthRequestType.AuthenticateAndUnlock;
        authRequest.OrganizationId = null;

        // Entity Framework Repo
        foreach (var efSut in efSuts)
        {
            var i = efSuts.IndexOf(efSut);

            // Create user
            var efUser = await efUserRepos[i].CreateAsync(user);
            efSut.ClearChangeTracking();

            userIdsToSearchOn.Add(efUser.Id);

            // Create device
            device.UserId = efUser.Id;
            device.Name = "test-ef-chrome";

            var efDevice = await efSuts[i].CreateAsync(device);
            efSut.ClearChangeTracking();

            // Create auth request
            authRequest.UserId = efUser.Id;
            authRequest.RequestDeviceIdentifier = efDevice.Identifier;

            // Old auth request
            authRequest.CreationDate = DateTime.UtcNow.AddMinutes(-10);
            await efAuthRequestRepos[i].CreateAsync(authRequest);

            // Fresher auth request
            authRequest.CreationDate = DateTime.UtcNow;
            correctAuthRequestsToRetrieve.Add((await efAuthRequestRepos[i].CreateAsync(authRequest)).Id);
        }

        // Dapper Repo
        // Create user
        var sqlUser = await sqlUserRepo.CreateAsync(user);

        userIdsToSearchOn.Add(sqlUser.Id);

        // Create device
        device.UserId = sqlUser.Id;
        device.Name = "test-sql-chrome";
        var sqlDevice = await sqlSut.CreateAsync(device);

        // Create auth request
        authRequest.UserId = sqlUser.Id;
        authRequest.Type = AuthRequestType.Unlock;
        authRequest.RequestDeviceIdentifier = sqlDevice.Identifier;

        // Old auth request
        authRequest.CreationDate = DateTime.UtcNow.AddMinutes(-10);
        await sqlAuthRequestRepository.CreateAsync(authRequest);

        // Fresher auth request
        authRequest.CreationDate = DateTime.UtcNow;
        correctAuthRequestsToRetrieve.Add((await sqlAuthRequestRepository.CreateAsync(authRequest)).Id);

        // Act

        // Entity Framework Responses
        foreach (var efSut in efSuts)
        {
            var i = efSuts.IndexOf(efSut);
            allResponses.Add(await efSut.GetManyByUserIdWithDeviceAuth(userIdsToSearchOn[i], expirationTime));
        }

        // Sql Responses
        allResponses.Add(await sqlSut.GetManyByUserIdWithDeviceAuth(userIdsToSearchOn.Last(), expirationTime));

        // Assert

        // Test all responses to not have a device pending auth request.
        // All but n-1 are EF responses. n is the stored procedure.
        foreach (var response in allResponses)
        {
            Assert.NotNull(response.First().DevicePendingAuthRequest);

            // Remove auth request id from the correct auth request pool.
            correctAuthRequestsToRetrieve.Remove(response.First().DevicePendingAuthRequest.Id);
        }

        // After we iterate through all our devices with auth requests and remove them from the expected list, there
        // should be none left.
        Assert.True(correctAuthRequestsToRetrieve.Count == 0);
    }

    [CiSkippedTheory, EfDeviceAutoData]
    public async Task GetManyByUserIdWithDeviceAuth_WorksWithNoAuthRequestAndMultipleDevices_ReturnsExpectedResults(
        Device device,
        User user,
        List<EfRepo.DeviceRepository> efSuts,
        List<EfRepo.UserRepository> efUserRepos,
        SqlRepo.DeviceRepository sqlSut,
        SqlRepo.UserRepository sqlUserRepo)
    {
        // Arrange
        var allResponses = new List<ICollection<DeviceAuthRequestResponseModel>>();
        var userIdsToSearchOn = new List<Guid>();
        var expirationTime = 15;

        // Configure data for successful responses.
        device.Active = true;

        // Entity Framework Repo
        foreach (var efSut in efSuts)
        {
            var i = efSuts.IndexOf(efSut);

            // Create user
            var efUser = await efUserRepos[i].CreateAsync(user);
            efSut.ClearChangeTracking();

            userIdsToSearchOn.Add(efUser.Id);

            // Create device
            device.UserId = efUser.Id;

            device.Identifier = Guid.NewGuid().ToString();
            device.Name = "test-ef-chrome";
            await efSuts[i].CreateAsync(device);

            device.Identifier = Guid.NewGuid().ToString();
            device.Name = "test-ef-chrome-2";
            await efSuts[i].CreateAsync(device);
        }

        // Dapper Repo
        // Create user
        var sqlUser = await sqlUserRepo.CreateAsync(user);

        userIdsToSearchOn.Add(sqlUser.Id);

        // Create device
        device.UserId = sqlUser.Id;

        device.Identifier = Guid.NewGuid().ToString();
        device.Name = "test-sql-chrome";
        await sqlSut.CreateAsync(device);

        device.Identifier = Guid.NewGuid().ToString();
        device.Name = "test-ef-chrome-2";
        await sqlSut.CreateAsync(device);

        // Act

        // Entity Framework Responses
        foreach (var efSut in efSuts)
        {
            var i = efSuts.IndexOf(efSut);
            allResponses.Add(await efSut.GetManyByUserIdWithDeviceAuth(userIdsToSearchOn[i], expirationTime));
        }

        // Sql Responses
        allResponses.Add(await sqlSut.GetManyByUserIdWithDeviceAuth(userIdsToSearchOn.Last(), expirationTime));

        // Assert

        // Test all responses to not have a device pending auth request.
        // All but n-1 are EF responses. n is the stored procedure.
        foreach (var response in allResponses)
        {
            Assert.NotNull(response.First());
            Assert.Null(response.First().DevicePendingAuthRequest);
            Assert.True(response.Count == 2);
        }
    }

    [CiSkippedTheory, EfDeviceAutoData]
    public async Task GetManyByUserIdWithDeviceAuth_Fails_ReturnsExpectedResults(
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
        var casesThatCauseNoAuthDataInResponse = new[]
        {
            new
            {
                authRequestType = AuthRequestType.AdminApproval, // Device typing is wrong
                authRequestApproved = (bool?)null,
                expirey = DateTime.UtcNow.AddMinutes(0),
            },
            new
            {
                authRequestType = AuthRequestType.AuthenticateAndUnlock,
                authRequestApproved = (bool?)true, // Auth request is already approved
                expirey = DateTime.UtcNow.AddMinutes(0),
            },
            new
            {
                authRequestType = AuthRequestType.AuthenticateAndUnlock,
                authRequestApproved = (bool?)null,
                expirey = DateTime.UtcNow.AddMinutes(-30), // Past the point of expiring
            }
        };

        foreach (var testCase in casesThatCauseNoAuthDataInResponse)
        {
            // Arrange
            var allResponses = new List<ICollection<DeviceAuthRequestResponseModel>>();
            var userIdsToSearchOn = new List<Guid>();
            const int expirationTime = 15;

            // Configure data for successful responses.
            user.Email = $"{user.Id.ToString().Substring(0, 5)}@test.com";

            device.Active = true;

            authRequest.ResponseDeviceId = null;
            authRequest.Type = testCase.authRequestType;
            authRequest.Approved = testCase.authRequestApproved;
            authRequest.OrganizationId = null;

            // Entity Framework Repo
            foreach (var efSut in efSuts)
            {
                var i = efSuts.IndexOf(efSut);

                // Create user
                var efUser = await efUserRepos[i].CreateAsync(user);
                efSut.ClearChangeTracking();

                userIdsToSearchOn.Add(efUser.Id);

                // Create device
                device.UserId = efUser.Id;
                device.Name = "test-ef-chrome";

                var efDevice = await efSuts[i].CreateAsync(device);
                efSut.ClearChangeTracking();

                // Create auth request
                authRequest.UserId = efUser.Id;
                authRequest.RequestDeviceIdentifier = efDevice.Identifier;
                authRequest.CreationDate = testCase.expirey;
                await efAuthRequestRepos[i].CreateAsync(authRequest);
            }

            // Dapper Repo
            // Create user
            var sqlUser = await sqlUserRepo.CreateAsync(user);

            userIdsToSearchOn.Add(sqlUser.Id);

            // Create device
            device.UserId = sqlUser.Id;
            device.Name = "test-sql-chrome";
            var sqlDevice = await sqlSut.CreateAsync(device);

            // Create auth request
            authRequest.UserId = sqlUser.Id;
            authRequest.RequestDeviceIdentifier = sqlDevice.Identifier;
            authRequest.CreationDate = testCase.expirey;
            await sqlAuthRequestRepository.CreateAsync(authRequest);

            // Act

            // Entity Framework Responses
            foreach (var efSut in efSuts)
            {
                var i = efSuts.IndexOf(efSut);
                allResponses.Add(await efSut.GetManyByUserIdWithDeviceAuth(userIdsToSearchOn[i], expirationTime));
            }

            // Sql Responses
            allResponses.Add(await sqlSut.GetManyByUserIdWithDeviceAuth(userIdsToSearchOn.Last(), expirationTime));

            // Assert

            // Test all responses to not have a device pending auth request.
            // All but n-1 are EF responses. n is the stored procedure.
            foreach (var response in allResponses)
            {
                Assert.Null(response.First().DevicePendingAuthRequest);
            }
        }
    }
}
