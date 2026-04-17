using Bit.Core.Auth.Entities;
using Bit.Core.Auth.Enums;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using Xunit;

namespace Bit.Infrastructure.IntegrationTest.Auth.Repositories;

public class DeviceRepositoryTests
{
    /// <summary>
    /// Verifies that all DeviceAuthDetails fields are correctly populated from the database,
    /// and that when multiple pending auth requests exist for the same device, only the most
    /// recent one is returned.
    /// </summary>
    [DatabaseTheory]
    [DatabaseData]
    public async Task ReplaceAsync_WithNullLastActivityDate_PreservesExistingValue(
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

        var device = await sutRepository.CreateAsync(new Device
        {
            Active = true,
            Name = "chrome-test",
            UserId = user.Id,
            Type = DeviceType.ChromeBrowser,
            Identifier = Guid.NewGuid().ToString(),
        });

        await sutRepository.BumpLastActivityDateByIdAsync(device.Id);
        var afterBump = await sutRepository.GetByIdAsync(device.Id);
        Assert.NotNull(afterBump!.LastActivityDate);

        // Act — ReplaceAsync with LastActivityDate = null should not overwrite the bumped value
        afterBump.LastActivityDate = null;
        await sutRepository.ReplaceAsync(afterBump);

        // Assert
        var afterReplace = await sutRepository.GetByIdAsync(device.Id);
        Assert.NotNull(afterReplace!.LastActivityDate);
    }

    [DatabaseTheory]
    [DatabaseData]
    public async Task ReplaceAsync_WithStaleLastActivityDate_PreservesNewerExistingValue(
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

        var device = await sutRepository.CreateAsync(new Device
        {
            Active = true,
            Name = "chrome-test",
            UserId = user.Id,
            Type = DeviceType.ChromeBrowser,
            Identifier = Guid.NewGuid().ToString(),
        });

        await sutRepository.BumpLastActivityDateByIdAsync(device.Id);
        var afterBump = await sutRepository.GetByIdAsync(device.Id);
        var bumpedDate = afterBump!.LastActivityDate;
        Assert.NotNull(bumpedDate);

        // Act — ReplaceAsync with a stale (older) LastActivityDate should not overwrite the newer bumped value
        afterBump.LastActivityDate = bumpedDate.Value.AddDays(-1);
        await sutRepository.ReplaceAsync(afterBump);

        // Assert
        var afterReplace = await sutRepository.GetByIdAsync(device.Id);
        Assert.Equal(bumpedDate, afterReplace!.LastActivityDate);
    }

    [DatabaseTheory]
    [DatabaseData]
    public async Task BumpLastActivityDateByIdentifierAndUserIdAsync_SetsLastActivityDate(
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

        var device = await sutRepository.CreateAsync(new Device
        {
            Active = true,
            Name = "chrome-test",
            UserId = user.Id,
            Type = DeviceType.ChromeBrowser,
            Identifier = Guid.NewGuid().ToString(),
        });

        // Act
        await sutRepository.BumpLastActivityDateByIdentifierAndUserIdAsync(device.Identifier, user.Id);

        // Assert
        var after = await sutRepository.GetByIdAsync(device.Id);
        Assert.NotNull(after!.LastActivityDate);
    }

    [DatabaseTheory]
    [DatabaseData]
    public async Task BumpLastActivityDateByIdentifierAndUserIdAsync_DoesNotBumpOtherUsersDevice(
        IDeviceRepository sutRepository,
        IUserRepository userRepository)
    {
        // Arrange — two users share the same device identifier
        var userA = await userRepository.CreateAsync(new User
        {
            Name = "Test User A",
            Email = $"test_user_A+{Guid.NewGuid()}@email.com",
            ApiKey = "TEST",
            SecurityStamp = "stamp",
        });

        var userB = await userRepository.CreateAsync(new User
        {
            Name = "Test User B",
            Email = $"test_user_B+{Guid.NewGuid()}@email.com",
            ApiKey = "TEST",
            SecurityStamp = "stamp",
        });

        var sharedIdentifier = Guid.NewGuid().ToString();

        var deviceA = await sutRepository.CreateAsync(new Device
        {
            Active = true,
            Name = "chrome-test",
            UserId = userA.Id,
            Type = DeviceType.ChromeBrowser,
            Identifier = sharedIdentifier,
        });

        var deviceB = await sutRepository.CreateAsync(new Device
        {
            Active = true,
            Name = "chrome-test",
            UserId = userB.Id,
            Type = DeviceType.ChromeBrowser,
            Identifier = sharedIdentifier,
        });

        var beforeB = (await sutRepository.GetByIdAsync(deviceB.Id))!.LastActivityDate;

        // Act — bump only userA's device
        await sutRepository.BumpLastActivityDateByIdentifierAndUserIdAsync(sharedIdentifier, userA.Id);

        // Assert — userA's device is bumped, userB's is unchanged
        var afterA = await sutRepository.GetByIdAsync(deviceA.Id);
        var afterB = await sutRepository.GetByIdAsync(deviceB.Id);
        Assert.NotNull(afterA!.LastActivityDate);
        Assert.Equal(beforeB, afterB!.LastActivityDate);
    }

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
            PushToken = "push-token",
            EncryptedUserKey = "encrypted-user-key",
            EncryptedPublicKey = "encrypted-public-key",
            EncryptedPrivateKey = "encrypted-private-key",
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
        var result = response.First();

        // Assert — device fields
        Assert.Equal(device.Id, result.Id);
        Assert.Equal(device.UserId, result.UserId);
        Assert.Equal(device.Name, result.Name);
        Assert.Equal(device.Type, result.Type);
        Assert.Equal(device.Identifier, result.Identifier);
        Assert.Equal(device.PushToken, result.PushToken);
        Assert.NotEqual(default, result.CreationDate);
        Assert.NotEqual(default, result.RevisionDate);
        Assert.Equal(device.EncryptedUserKey, result.EncryptedUserKey);
        Assert.Equal(device.EncryptedPublicKey, result.EncryptedPublicKey);
        Assert.Equal(device.EncryptedPrivateKey, result.EncryptedPrivateKey);
        Assert.Equal(device.Active, result.Active);
        // Assert — most recent pending auth request is returned, not the stale one
        Assert.Equal(freshAuthRequest.Id, result.AuthRequestId);
        Assert.NotNull(result.AuthRequestCreationDate);
    }

    /// <summary>
    /// Verifies that when two users share the same device identifier, a pending auth request
    /// belonging to one user does not appear on the other user's device results.
    /// </summary>
    [DatabaseTheory]
    [DatabaseData]
    public async Task GetManyByUserIdWithDeviceAuth_WorksWithMultipleUsersOnSameDevice_ReturnsExpectedResults(
        IDeviceRepository sutRepository,
        IUserRepository userRepository,
        IAuthRequestRepository authRequestRepository)
    {
        // Arrange
        var userA = await userRepository.CreateAsync(new User
        {
            Name = "Test User A",
            Email = $"test_user_A+{Guid.NewGuid()}@email.com",
            ApiKey = "TEST",
            SecurityStamp = "stamp",
        });

        var userB = await userRepository.CreateAsync(new User
        {
            Name = "Test User B",
            Email = $"test_user_B+{Guid.NewGuid()}@email.com",
            ApiKey = "TEST",
            SecurityStamp = "stamp",
        });

        var sharedDeviceIdentifier = Guid.NewGuid().ToString();

        var deviceForUserA = await sutRepository.CreateAsync(new Device
        {
            Active = true,
            Name = "chrome-test",
            UserId = userA.Id,
            Type = DeviceType.ChromeBrowser,
            Identifier = sharedDeviceIdentifier,
        });

        var deviceForUserB = await sutRepository.CreateAsync(new Device
        {
            Active = true,
            Name = "chrome-test",
            UserId = userB.Id,
            Type = DeviceType.ChromeBrowser,
            Identifier = sharedDeviceIdentifier,
        });

        var userAAuthRequest = await authRequestRepository.CreateAsync(new AuthRequest
        {
            ResponseDeviceId = null,
            Approved = null,
            Type = AuthRequestType.AuthenticateAndUnlock,
            OrganizationId = null,
            UserId = userA.Id,
            RequestIpAddress = ":1",
            RequestDeviceIdentifier = deviceForUserA.Identifier,
            AccessCode = "AccessCode_1234",
            PublicKey = "PublicKey_1234"
        });

        // Act
        var response = await sutRepository.GetManyByUserIdWithDeviceAuth(userB.Id);

        // Assert
        Assert.Null(response.First().AuthRequestId);
        Assert.Null(response.First().AuthRequestCreationDate);
    }

    /// <summary>
    /// Verifies that all active devices for a user are returned even when none have
    /// a pending auth request, and that AuthRequestId is null in that case.
    /// </summary>
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
        Assert.Null(response.First().AuthRequestId);
        Assert.Equal(2, response.Count);
    }

    /// <summary>
    /// Verifies that IsTrusted is computed from the presence of all three encrypted keys
    /// (EncryptedUserKey, EncryptedPublicKey, EncryptedPrivateKey) and is not a stored column.
    /// </summary>
    [DatabaseTheory]
    [DatabaseData]
    public async Task GetManyByUserIdWithDeviceAuth_IsTrustedComputedCorrectlyAsync(
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

        var trustedDevice = await sutRepository.CreateAsync(new Device
        {
            Active = true,
            Name = "trusted-device",
            UserId = user.Id,
            Type = DeviceType.ChromeBrowser,
            Identifier = Guid.NewGuid().ToString(),
            EncryptedUserKey = "encrypted-user-key",
            EncryptedPublicKey = "encrypted-public-key",
            EncryptedPrivateKey = "encrypted-private-key",
        });

        var untrustedDevice = await sutRepository.CreateAsync(new Device
        {
            Active = true,
            Name = "untrusted-device",
            UserId = user.Id,
            Type = DeviceType.ChromeBrowser,
            Identifier = Guid.NewGuid().ToString(),
        });

        // Act
        var response = await sutRepository.GetManyByUserIdWithDeviceAuth(user.Id);

        // Assert — IsTrusted is computed from encrypted keys, not stored as a database column
        Assert.True(response.First(d => d.Id == trustedDevice.Id).IsTrusted);
        Assert.False(response.First(d => d.Id == untrustedDevice.Id).IsTrusted);
    }

    /// <summary>
    /// Verifies that auth requests which are ineligible — wrong type (AdminApproval),
    /// already approved, or expired — do not populate AuthRequestId or AuthRequestCreationDate.
    /// The device itself is still returned; only the auth request fields are null.
    /// </summary>
    [DatabaseTheory]
    [DatabaseData]
    public async Task GetManyByUserIdWithDeviceAuth_IneligibleAuthRequests_ReturnsDeviceWithNullAuthDataAsync(
        IDeviceRepository sutRepository,
        IUserRepository userRepository,
        IAuthRequestRepository authRequestRepository)
    {
        var casesThatCauseNoAuthDataInResponse = new[]
        {
            new
            {
                // Only AuthenticateAndUnlock and Unlock types are eligible for pending auth request matching
                // AdminApproval is not eligible, even if it's pending
                authRequestType = AuthRequestType.AdminApproval,
                authRequestApproved = (bool?)null,
                expiry = DateTime.UtcNow.AddMinutes(0),
            },
            new
            {
                authRequestType = AuthRequestType.AuthenticateAndUnlock,
                authRequestApproved = (bool?)true, // Auth request is already approved
                expiry = DateTime.UtcNow.AddMinutes(0),
            },
            new
            {
                authRequestType = AuthRequestType.AuthenticateAndUnlock,
                authRequestApproved = (bool?)null,
                expiry = DateTime.UtcNow.AddMinutes(-30), // Past the point of expiring
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

            authRequest.CreationDate = testCase.expiry;
            await authRequestRepository.ReplaceAsync(authRequest);

            // Act
            var response = await sutRepository.GetManyByUserIdWithDeviceAuth(user.Id);

            // Assert — device is returned but with no auth request data
            Assert.Single(response);
            Assert.Null(response.First().AuthRequestId);
            Assert.Null(response.First().AuthRequestCreationDate);
        }
    }

    /// <summary>
    /// Verifies that the Unlock auth request type is treated as a valid pending request
    /// and populates AuthRequestId and AuthRequestCreationDate on the device response.
    /// </summary>
    [DatabaseTheory]
    [DatabaseData]
    public async Task GetManyByUserIdWithDeviceAuth_UnlockAuthRequestType_ReturnsAuthDataAsync(
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

        var authRequest = await authRequestRepository.CreateAsync(new AuthRequest
        {
            ResponseDeviceId = null,
            Approved = null,
            Type = AuthRequestType.Unlock,
            OrganizationId = null,
            UserId = user.Id,
            RequestIpAddress = ":1",
            RequestDeviceIdentifier = device.Identifier,
            AccessCode = "AccessCode_1234",
            PublicKey = "PublicKey_1234",
        });

        // Act
        var response = await sutRepository.GetManyByUserIdWithDeviceAuth(user.Id);

        // Assert — Unlock type (1) is treated as a valid pending auth request type and populates auth data on the device response
        Assert.Equal(authRequest.Id, response.First().AuthRequestId);
        Assert.NotNull(response.First().AuthRequestCreationDate);
    }

    /// <summary>
    /// Verifies that devices with Active = false are excluded from results. Only active
    /// devices should be returned, regardless of whether they have a pending auth request.
    /// </summary>
    [Theory]
    [DatabaseData]
    public async Task GetManyByUserIdWithDeviceAuth_InactiveDevice_IsExcludedFromResultsAsync(
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

        var activeDevice = await sutRepository.CreateAsync(new Device
        {
            Active = true,
            Name = "active-device",
            UserId = user.Id,
            Type = DeviceType.ChromeBrowser,
            Identifier = Guid.NewGuid().ToString(),
        });

        await sutRepository.CreateAsync(new Device
        {
            Active = false,
            Name = "inactive-device",
            UserId = user.Id,
            Type = DeviceType.ChromeBrowser,
            Identifier = Guid.NewGuid().ToString(),
        });

        // Act
        var response = await sutRepository.GetManyByUserIdWithDeviceAuth(user.Id);

        // Assert — only the active device is returned
        Assert.Single(response);
        Assert.Equal(activeDevice.Id, response.First().Id);
    }

    /// <summary>
    /// Verifies that LastActivityDate is correctly returned from GetManyByUserIdWithDeviceAuth
    /// and matches the value set by BumpLastActivityDateByIdAsync.
    /// </summary>
    [DatabaseTheory]
    [DatabaseData]
    public async Task GetManyByUserIdWithDeviceAuth_ReturnsLastActivityDate_WhenBumped(
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

        var device = await sutRepository.CreateAsync(new Device
        {
            Active = true,
            Name = "chrome-test",
            UserId = user.Id,
            Type = DeviceType.ChromeBrowser,
            Identifier = Guid.NewGuid().ToString(),
        });

        await sutRepository.BumpLastActivityDateByIdAsync(device.Id);
        var afterBump = await sutRepository.GetByIdAsync(device.Id);
        var expectedLastActivityDate = afterBump!.LastActivityDate;
        Assert.NotNull(expectedLastActivityDate);

        // Act
        var response = await sutRepository.GetManyByUserIdWithDeviceAuth(user.Id);
        var result = response.Single();

        // Assert — LastActivityDate from the stored procedure must match the bumped value,
        // not null and not the C# property initializer default (DateTime.UtcNow).
        Assert.Equal(expectedLastActivityDate, result.LastActivityDate);
    }

    /// <summary>
    /// Verifies that LastActivityDate is non-null for a newly created device when returned from
    /// GetManyByUserIdWithDeviceAuth. Device creation sets LastActivityDate to the current time
    /// so users see a meaningful date immediately on their devices screen.
    /// </summary>
    [DatabaseTheory]
    [DatabaseData]
    public async Task GetManyByUserIdWithDeviceAuth_ReturnsLastActivityDate_ForNewDeviceAsync(
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

        var beforeCreation = DateTime.UtcNow;

        await sutRepository.CreateAsync(new Device
        {
            Active = true,
            Name = "chrome-test",
            UserId = user.Id,
            Type = DeviceType.ChromeBrowser,
            Identifier = Guid.NewGuid().ToString(),
        });

        // Act
        var response = await sutRepository.GetManyByUserIdWithDeviceAuth(user.Id);
        var result = response.Single();

        // Assert — LastActivityDate is set at creation time and returned by the stored procedure
        Assert.NotNull(result.LastActivityDate);
    }

    /// <summary>
    /// Verifies that a user with no registered devices receives an empty collection,
    /// not null or an error.
    /// </summary>
    [Theory]
    [DatabaseData]
    public async Task GetManyByUserIdWithDeviceAuth_UserWithNoDevices_ReturnsEmptyListAsync(
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

        // Act
        var response = await sutRepository.GetManyByUserIdWithDeviceAuth(user.Id);

        // Assert
        Assert.Empty(response);
    }
}
