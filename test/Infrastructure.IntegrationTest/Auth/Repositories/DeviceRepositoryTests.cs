using Bit.Core.Auth.Entities;
using Bit.Core.Auth.Enums;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using Xunit;

namespace Bit.Infrastructure.IntegrationTest.Auth.Repositories;

public class DeviceRepositoryTests
{
    [DatabaseTheory]
    [DatabaseData]
    public async Task GetManyByUserIdWithDeviceAuth_Works_ReturnsExpectedResults(
        IDeviceRepository sutRepository,
        IUserRepository userRepository,
        IAuthRequestRepository authRequestRepository)
    {
        // Arrange
        var user = await userRepository.CreateAsync(new User
        {
            Name = "Test User",
            Email = $"test+{Guid.NewGuid()}@email.com",
            ApiKey = "TEST",
            SecurityStamp = "stamp",
        });

        var device = await sutRepository.CreateAsync(new Device
        {
            Active = true,
            Name = "chrome-test",
            UserId = user.Id,
            Type = DeviceType.ChromeBrowser,
            Identifier = Guid.NewGuid().ToString(),
        });

        var staleAuthRequest = await authRequestRepository.CreateAsync(new AuthRequest
        {
            ResponseDeviceId = null,
            Approved = null,
            Type = AuthRequestType.AuthenticateAndUnlock,
            OrganizationId = null,
            UserId = user.Id,
            RequestIpAddress = ":1",
            RequestDeviceIdentifier = device.Identifier,
            AccessCode = "AccessCode_1234",
            PublicKey = "PublicKey_1234"
        });
        staleAuthRequest.CreationDate = DateTime.UtcNow.AddMinutes(-10);
        await authRequestRepository.ReplaceAsync(staleAuthRequest);

        var freshAuthRequest = await authRequestRepository.CreateAsync(new AuthRequest
        {
            ResponseDeviceId = null,
            Approved = null,
            Type = AuthRequestType.AuthenticateAndUnlock,
            OrganizationId = null,
            UserId = user.Id,
            RequestIpAddress = ":1",
            RequestDeviceIdentifier = device.Identifier,
            AccessCode = "AccessCode_1234",
            PublicKey = "PublicKey_1234",
            Key = "Key_1234",
            MasterPasswordHash = "MasterPasswordHash_1234"
        });

        // Act
        var response = await sutRepository.GetManyByUserIdWithDeviceAuth(user.Id);

        // Assert
        Assert.NotNull(response.First().AuthRequestId);
        Assert.NotNull(response.First().AuthRequestCreatedAt);
        Assert.Equal(response.First().AuthRequestId, freshAuthRequest.Id);
    }

    [DatabaseTheory]
    [DatabaseData]
    public async Task GetManyByUserIdWithDeviceAuth_WorksWithNoAuthRequestAndMultipleDevices_ReturnsExpectedResults(
        IDeviceRepository sutRepository,
        IUserRepository userRepository)
    {
        // Arrange
        var user = await userRepository.CreateAsync(new User
        {
            Name = "Test User",
            Email = $"test+{Guid.NewGuid()}@email.com",
            ApiKey = "TEST",
            SecurityStamp = "stamp",
        });

        await sutRepository.CreateAsync(new Device
        {
            Active = true,
            Name = "chrome-test",
            UserId = user.Id,
            Type = DeviceType.ChromeBrowser,
            Identifier = Guid.NewGuid().ToString(),
        });

        await sutRepository.CreateAsync(new Device
        {
            Active = true,
            Name = "macos-test",
            UserId = user.Id,
            Type = DeviceType.MacOsDesktop,
            Identifier = Guid.NewGuid().ToString(),
        });

        // Act
        var response = await sutRepository.GetManyByUserIdWithDeviceAuth(user.Id);

        // Assert
        Assert.NotNull(response.First());
        Assert.Null(response.First().AuthRequestId);
        Assert.True(response.Count == 2);
    }

    [DatabaseTheory]
    [DatabaseData]
    public async Task GetManyByUserIdWithDeviceAuth_FailsToRespondWithAnyAuthData_ReturnsExpectedResults(
        IDeviceRepository sutRepository,
        IUserRepository userRepository,
        IAuthRequestRepository authRequestRepository)
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
            var user = await userRepository.CreateAsync(new User
            {
                Name = "Test User",
                Email = $"test+{Guid.NewGuid()}@email.com",
                ApiKey = "TEST",
                SecurityStamp = "stamp",
            });

            var device = await sutRepository.CreateAsync(new Device
            {
                Active = true,
                Name = "chrome-test",
                UserId = user.Id,
                Type = DeviceType.ChromeBrowser,
                Identifier = Guid.NewGuid().ToString(),
            });

            var authRequest = await authRequestRepository.CreateAsync(new AuthRequest
            {
                ResponseDeviceId = null,
                Approved = testCase.authRequestApproved,
                Type = testCase.authRequestType,
                OrganizationId = null,
                UserId = user.Id,
                RequestIpAddress = ":1",
                RequestDeviceIdentifier = device.Identifier,
                AccessCode = "AccessCode_1234",
                PublicKey = "PublicKey_1234"
            });

            authRequest.CreationDate = testCase.expirey;
            await authRequestRepository.ReplaceAsync(authRequest);

            // Act
            var response = await sutRepository.GetManyByUserIdWithDeviceAuth(user.Id);

            // Assert
            Assert.Null(response.First().AuthRequestId);
            Assert.Null(response.First().AuthRequestCreatedAt);
        }
    }
}
