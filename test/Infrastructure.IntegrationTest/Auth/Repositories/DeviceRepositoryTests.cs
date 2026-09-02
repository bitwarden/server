using Bit.Core.Auth.Entities;
using Bit.Core.Auth.Enums;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using Xunit;

namespace Bit.Infrastructure.IntegrationTest.Auth.Repositories;

public class DeviceRepositoryTests
{
    // -------------------------------------------------------------------------------------------
    // Test helpers
    // -------------------------------------------------------------------------------------------

    private static async Task<User> CreateTestUserAsync(
        IUserRepository userRepository,
        string nameSuffix = "")
    {
        return await userRepository.CreateAsync(new User
        {
            Name = string.IsNullOrEmpty(nameSuffix) ? "Test User" : $"Test User {nameSuffix}",
            Email = $"test+{Guid.NewGuid()}@email.com",
            ApiKey = "TEST",
            SecurityStamp = "stamp",
        });
    }

    private static async Task<Device> CreateTestDeviceAsync(
        IDeviceRepository sutRepository,
        Guid userId,
        string clientVersion = null,
        string identifier = null,
        string name = "chrome-test",
        DeviceType type = DeviceType.ChromeBrowser,
        bool active = true)
    {
        return await sutRepository.CreateAsync(new Device
        {
            Active = active,
            Name = name,
            UserId = userId,
            Type = type,
            Identifier = identifier ?? Guid.NewGuid().ToString(),
            ClientVersion = clientVersion,
            // Mirror production creation sites — device creation counts as first activity.
            LastActivityDate = DateTime.UtcNow,
        });
    }

    /// <summary>
    /// Truncates a <see cref="DateTime"/> to second-level precision (preserving <see cref="DateTime.Kind"/>).
    /// Used to compare a SqlServer-round-tripped <c>DateTime</c> against an in-memory <c>DateTime.UtcNow</c>:
    /// Dapper binds <c>DateTime</c> parameters as legacy <c>datetime</c> (~3.33ms granularity), so the stored
    /// value can be a few ms earlier than the in-memory capture. Truncating to the second absorbs that drift
    /// while still detecting stale or defaulted values (which would be off by seconds, not milliseconds).
    /// </summary>
    private static DateTime TruncateToSecond(DateTime value) =>
        new(value.Ticks - value.Ticks % TimeSpan.TicksPerSecond, value.Kind);

    // -------------------------------------------------------------------------------------------
    // ReplaceAsync
    // -------------------------------------------------------------------------------------------

    /// <summary>
    /// Verifies the Device_Update NULL-passthrough guards: if a general save (ReplaceAsync) passes
    /// NULL for either last-activity column (LastActivityDate or ClientVersion), the stored value
    /// must be preserved. This covers both columns' guards in a single arrange/act/assert so a
    /// regression on either guard fails this test loudly.
    /// </summary>
    [DatabaseTheory]
    [DatabaseData]
    public async Task ReplaceAsync_WithNullLastActivityFields_PreservesExistingValues(
        IDeviceRepository sutRepository,
        IUserRepository userRepository)
    {
        // Arrange — device with a stored ClientVersion, then update to populate LastActivityDate too.
        var user = await CreateTestUserAsync(userRepository);
        var device = await CreateTestDeviceAsync(sutRepository, user.Id, clientVersion: "2026.5.1");

        await sutRepository.UpdateLastActivityByIdAsync(device.Id, clientVersion: null);
        var afterUpdate = await sutRepository.GetByIdAsync(device.Id);
        Assert.NotNull(afterUpdate!.LastActivityDate);
        Assert.Equal("2026.5.1", afterUpdate.ClientVersion);

        // Act — null out BOTH last-activity fields and ReplaceAsync; SP-side ISNULL / CASE guards
        // (and the EF-side IsModified=false in ReplaceAsync override) should preserve the stored values.
        afterUpdate.LastActivityDate = null;
        afterUpdate.ClientVersion = null;
        await sutRepository.ReplaceAsync(afterUpdate);

        // Assert — both columns preserved
        var afterReplace = await sutRepository.GetByIdAsync(device.Id);
        Assert.NotNull(afterReplace!.LastActivityDate);
        Assert.Equal("2026.5.1", afterReplace.ClientVersion);
    }

    [DatabaseTheory]
    [DatabaseData]
    public async Task ReplaceAsync_WithStaleLastActivityDate_PreservesNewerExistingValue(
        IDeviceRepository sutRepository,
        IUserRepository userRepository)
    {
        // Arrange
        var user = await CreateTestUserAsync(userRepository);
        var device = await CreateTestDeviceAsync(sutRepository, user.Id);

        await sutRepository.UpdateLastActivityByIdAsync(device.Id, clientVersion: null);
        var afterUpdate = await sutRepository.GetByIdAsync(device.Id);
        var updatedDate = afterUpdate!.LastActivityDate;
        Assert.NotNull(updatedDate);

        // Act — ReplaceAsync with a stale (older) LastActivityDate should not overwrite the newer value
        afterUpdate.LastActivityDate = updatedDate.Value.AddDays(-1);
        await sutRepository.ReplaceAsync(afterUpdate);

        // Assert
        var afterReplace = await sutRepository.GetByIdAsync(device.Id);
        Assert.Equal(updatedDate, afterReplace!.LastActivityDate);
    }

    /// <summary>
    /// Updating the last-activity state on a freshly-created device via the by-identifier path with
    /// no <c>ClientVersion</c> supplied at create or at update is a same-day no-op on both columns:
    /// the SP's day-level guard fires for <c>LastActivityDate</c> (already today via the entity
    /// initializer) and the NULL guard fires for <c>ClientVersion</c>. Locks in that the update
    /// path does not silently regress either column when both are already in their expected state.
    /// </summary>
    [DatabaseTheory]
    [DatabaseData]
    public async Task UpdateLastActivityByIdentifierAndUserIdAsync_OnFreshDeviceWithoutClientVersion_PreservesColumns(
        IDeviceRepository sutRepository,
        IUserRepository userRepository)
    {
        // Arrange
        var user = await CreateTestUserAsync(userRepository);
        var device = await CreateTestDeviceAsync(sutRepository, user.Id);
        var ladBefore = (await sutRepository.GetByIdAsync(device.Id)).LastActivityDate;

        // Act — update without a ClientVersion on a same-day fresh device
        await sutRepository.UpdateLastActivityByIdentifierAndUserIdAsync(device.Identifier, user.Id, clientVersion: null);

        // Assert — both columns preserved (LAD same-day; ClientVersion still null)
        var after = await sutRepository.GetByIdAsync(device.Id);
        Assert.Equal(ladBefore, after.LastActivityDate);
        Assert.Null(after.ClientVersion);
    }

    [DatabaseTheory]
    [DatabaseData]
    public async Task UpdateLastActivityByIdentifierAndUserIdAsync_DoesNotAffectOtherUsersDevice(
        IDeviceRepository sutRepository,
        IUserRepository userRepository)
    {
        // Arrange — two users share the same device identifier
        var userA = await CreateTestUserAsync(userRepository, "A");
        var userB = await CreateTestUserAsync(userRepository, "B");

        var sharedIdentifier = Guid.NewGuid().ToString();
        var deviceA = await CreateTestDeviceAsync(sutRepository, userA.Id, identifier: sharedIdentifier);
        var deviceB = await CreateTestDeviceAsync(sutRepository, userB.Id, identifier: sharedIdentifier);

        var beforeB = (await sutRepository.GetByIdAsync(deviceB.Id)).LastActivityDate;

        // Act — update only userA's device
        await sutRepository.UpdateLastActivityByIdentifierAndUserIdAsync(sharedIdentifier, userA.Id, clientVersion: null);

        // Assert — userA's device is updated, userB's is unchanged
        var afterA = await sutRepository.GetByIdAsync(deviceA.Id);
        var afterB = await sutRepository.GetByIdAsync(deviceB.Id);
        Assert.NotNull(afterA!.LastActivityDate);
        Assert.Equal(beforeB, afterB!.LastActivityDate);
    }

    // -------------------------------------------------------------------------------------------
    // GetManyByUserIdWithDeviceAuth
    // -------------------------------------------------------------------------------------------

    [DatabaseTheory]
    [DatabaseData]
    public async Task GetManyByUserIdWithDeviceAuth_Works_ReturnsExpectedResults(
        IDeviceRepository sutRepository,
        IUserRepository userRepository,
        IAuthRequestRepository authRequestRepository)
    {
        // Arrange
        var user = await CreateTestUserAsync(userRepository);

        // Custom fields (PushToken + Encrypted keys) — keep inline.
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
        var userA = await CreateTestUserAsync(userRepository, "A");
        var userB = await CreateTestUserAsync(userRepository, "B");

        var sharedDeviceIdentifier = Guid.NewGuid().ToString();
        var deviceForUserA = await CreateTestDeviceAsync(sutRepository, userA.Id, identifier: sharedDeviceIdentifier);
        var deviceForUserB = await CreateTestDeviceAsync(sutRepository, userB.Id, identifier: sharedDeviceIdentifier);

        // create userAAuthRequest
        await authRequestRepository.CreateAsync(new AuthRequest
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

        // Assert — userB gets exactly their own device back (not userA's), and userA's pending
        // auth request does not leak into userB's response.
        Assert.Single(response);
        Assert.Equal(deviceForUserB.Id, response.First().Id);
        Assert.Null(response.First().AuthRequestId);
        Assert.Null(response.First().AuthRequestCreationDate);
    }

    [DatabaseTheory]
    [DatabaseData]
    public async Task GetManyByUserIdWithDeviceAuth_WorksWithNoAuthRequestAndMultipleDevices_ReturnsExpectedResults(
        IDeviceRepository sutRepository,
        IUserRepository userRepository)
    {
        // Arrange
        var user = await CreateTestUserAsync(userRepository);
        await CreateTestDeviceAsync(sutRepository, user.Id);
        await CreateTestDeviceAsync(sutRepository, user.Id, name: "macos-test", type: DeviceType.MacOsDesktop);

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
        var user = await CreateTestUserAsync(userRepository);

        // Trusted device requires all three encrypted keys — keep inline.
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

        var untrustedDevice = await CreateTestDeviceAsync(sutRepository, user.Id, name: "untrusted-device");

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
            var user = await CreateTestUserAsync(userRepository);
            var device = await CreateTestDeviceAsync(sutRepository, user.Id);

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
        var user = await CreateTestUserAsync(userRepository);
        var device = await CreateTestDeviceAsync(sutRepository, user.Id);

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
        Assert.Single(response);
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
        var user = await CreateTestUserAsync(userRepository);
        var activeDevice = await CreateTestDeviceAsync(sutRepository, user.Id, name: "active-device");
        await CreateTestDeviceAsync(sutRepository, user.Id, name: "inactive-device", active: false);

        // Act
        var response = await sutRepository.GetManyByUserIdWithDeviceAuth(user.Id);

        // Assert — only the active device is returned
        Assert.Single(response);
        Assert.Equal(activeDevice.Id, response.First().Id);
    }

    /// <summary>
    /// Verifies that LastActivityDate is correctly returned from GetManyByUserIdWithDeviceAuth
    /// and matches the value set by UpdateLastActivityByIdAsync.
    /// </summary>
    [DatabaseTheory]
    [DatabaseData]
    public async Task GetManyByUserIdWithDeviceAuth_ReturnsLastActivityDate_WhenUpdated(
        IDeviceRepository sutRepository,
        IUserRepository userRepository)
    {
        // Arrange
        var user = await CreateTestUserAsync(userRepository);
        var device = await CreateTestDeviceAsync(sutRepository, user.Id);

        await sutRepository.UpdateLastActivityByIdAsync(device.Id, clientVersion: null);
        var afterUpdate = await sutRepository.GetByIdAsync(device.Id);
        var expectedLastActivityDate = afterUpdate!.LastActivityDate;
        Assert.NotNull(expectedLastActivityDate);

        // Act
        var response = await sutRepository.GetManyByUserIdWithDeviceAuth(user.Id);
        var result = response.Single();

        // Assert — LastActivityDate from the stored procedure must match the value written by the
        // update, not null and not the C# property initializer default (DateTime.UtcNow).
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
        var user = await CreateTestUserAsync(userRepository);
        var beforeCreation = DateTime.UtcNow;
        await CreateTestDeviceAsync(sutRepository, user.Id);

        // Act
        var response = await sutRepository.GetManyByUserIdWithDeviceAuth(user.Id);
        var result = response.Single();

        // Assert — LastActivityDate is set at creation time (>= beforeCreation) and returned by the
        // stored procedure. The >= check locks in that the entity initializer's DateTime.UtcNow
        // flowed through Device_Create rather than the column being set by some default later.
        // Compared at second-level precision to absorb Dapper's legacy-`datetime` rounding on
        // SqlServer — see TruncateToSecond for details.
        Assert.NotNull(result.LastActivityDate);
        Assert.True(TruncateToSecond(result.LastActivityDate.Value) >= TruncateToSecond(beforeCreation),
            $"LastActivityDate {result.LastActivityDate:O} precedes beforeCreation {beforeCreation:O} at second-level precision.");
    }

    /// <summary>
    /// Creates a device with an explicit null LastActivityDate and asserts the read path surfaces null verbatim.
    /// </summary>
    [DatabaseTheory]
    [DatabaseData]
    public async Task GetManyByUserIdWithDeviceAuth_NullLastActivityDateInDb_ReturnsNullNotDefaultAsync(
        IDeviceRepository sutRepository,
        IUserRepository userRepository)
    {
        // Arrange
        var user = await CreateTestUserAsync(userRepository);
        await sutRepository.CreateAsync(new Device
        {
            UserId = user.Id,
            Name = "legacy-null-activity",
            Type = DeviceType.ChromeBrowser,
            Identifier = Guid.NewGuid().ToString(),
            LastActivityDate = null,
        });

        // Act
        var response = await sutRepository.GetManyByUserIdWithDeviceAuth(user.Id);

        // Assert
        var result = Assert.Single(response);
        Assert.Null(result.LastActivityDate);
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
        var user = await CreateTestUserAsync(userRepository);

        // Act
        var response = await sutRepository.GetManyByUserIdWithDeviceAuth(user.Id);

        // Assert
        Assert.Empty(response);
    }

    // -------------------------------------------------------------------------------------------
    // UpdateLastActivityByIdAsync
    // -------------------------------------------------------------------------------------------

    /// <summary>
    /// Updating with a different <c>ClientVersion</c> writes only that column when the day-level
    /// guard is otherwise satisfied. Asserts both columns to catch cross-column regressions — the
    /// SP/EF query writes both axes via a composite WHERE, so asserting one in isolation could miss
    /// interaction bugs. <c>LastActivityDate</c> is unchanged here because <c>CreateAsync</c> sets
    /// it to <c>DateTime.UtcNow</c> via the entity initializer, so a same-day update's day-level
    /// guard already evaluates false.
    /// </summary>
    [DatabaseTheory]
    [DatabaseData]
    public async Task UpdateLastActivityByIdAsync_VersionChanged_UpdatesClientVersion(
        IDeviceRepository sutRepository,
        IUserRepository userRepository)
    {
        // Arrange
        var user = await CreateTestUserAsync(userRepository);
        var device = await CreateTestDeviceAsync(sutRepository, user.Id, clientVersion: "2026.4.0");
        var ladBefore = (await sutRepository.GetByIdAsync(device.Id)).LastActivityDate;

        // Act
        await sutRepository.UpdateLastActivityByIdAsync(device.Id, "2026.5.1");

        // Assert
        var after = await sutRepository.GetByIdAsync(device.Id);
        Assert.Equal("2026.5.1", after!.ClientVersion);
        Assert.Equal(ladBefore, after.LastActivityDate);
    }

    /// <summary>
    /// Updating with the same <c>ClientVersion</c> on a device whose <c>LastActivityDate</c> is
    /// already today is a no-op: the composite WHERE evaluates false on both axes. Locks in that
    /// neither column is touched when nothing needs to change.
    /// </summary>
    [DatabaseTheory]
    [DatabaseData]
    public async Task UpdateLastActivityByIdAsync_VersionUnchanged_DoesNotUpdate(
        IDeviceRepository sutRepository,
        IUserRepository userRepository)
    {
        // Arrange — set the device to today's LastActivityDate AND a fixed ClientVersion. Then updating
        // with the same version should be a no-op (composite WHERE evaluates false).
        var user = await CreateTestUserAsync(userRepository);
        var device = await CreateTestDeviceAsync(sutRepository, user.Id, clientVersion: "2026.5.1");

        // Run once with the same version to force LastActivityDate to "today" (so the day-level
        // guard also returns false on the second call).
        await sutRepository.UpdateLastActivityByIdAsync(device.Id, "2026.5.1");
        var afterFirstUpdate = await sutRepository.GetByIdAsync(device.Id);
        var lastActivityAfterFirstUpdate = afterFirstUpdate!.LastActivityDate;

        // Act — running again with the same version should be a no-op
        await sutRepository.UpdateLastActivityByIdAsync(device.Id, "2026.5.1");

        // Assert — neither column changed
        var afterSecondUpdate = await sutRepository.GetByIdAsync(device.Id);
        Assert.Equal("2026.5.1", afterSecondUpdate!.ClientVersion);
        Assert.Equal(lastActivityAfterFirstUpdate, afterSecondUpdate.LastActivityDate);
    }

    /// <summary>
    /// Updating with a null <c>ClientVersion</c> (e.g. client missing the header) must not clobber
    /// a stored value — the per-column NULL guard preserves it. <c>LastActivityDate</c> is also
    /// unchanged here because the day-level guard already evaluates false (LAD is today from
    /// <c>CreateAsync</c>).
    /// </summary>
    [DatabaseTheory]
    [DatabaseData]
    public async Task UpdateLastActivityByIdAsync_VersionNull_LeavesClientVersionAlone(
        IDeviceRepository sutRepository,
        IUserRepository userRepository)
    {
        // Arrange
        var user = await CreateTestUserAsync(userRepository);
        var device = await CreateTestDeviceAsync(sutRepository, user.Id, clientVersion: "2026.5.1");
        var ladBefore = (await sutRepository.GetByIdAsync(device.Id)).LastActivityDate;

        // Act — call with a null version (e.g. client missing the header)
        await sutRepository.UpdateLastActivityByIdAsync(device.Id, clientVersion: null);

        // Assert
        var after = await sutRepository.GetByIdAsync(device.Id);
        Assert.Equal("2026.5.1", after!.ClientVersion);
        Assert.Equal(ladBefore, after.LastActivityDate);
    }

    /// <summary>
    /// Verifies that a stale-version update writes <c>ClientVersion</c> and does not regress
    /// <c>LastActivityDate</c> (LAD never moves backwards as a side effect of a version-only
    /// update). LAD is "today" here because <c>CreateAsync</c>'s entity initializer set it; the
    /// SP's day-level guard means LAD itself isn't written in this scenario, only CV is.
    /// </summary>
    [DatabaseTheory]
    [DatabaseData]
    public async Task UpdateLastActivityByIdAsync_StaleVersion_UpdatesVersionWithoutRegressingLastActivityDate(
        IDeviceRepository sutRepository,
        IUserRepository userRepository)
    {
        // Arrange — device with an old ClientVersion. (LastActivityDate is set to "now" by the
        // entity initializer at CreateAsync; we capture the pre-update value to confirm the per-column
        // guard correctly evaluates the day boundary on the update call.)
        var user = await CreateTestUserAsync(userRepository);
        var device = await CreateTestDeviceAsync(sutRepository, user.Id, clientVersion: "2026.4.0");
        var ladBefore = (await sutRepository.GetByIdAsync(device.Id)).LastActivityDate;

        // Act
        await sutRepository.UpdateLastActivityByIdAsync(device.Id, "2026.5.1");

        // Assert — ClientVersion updated; LastActivityDate is still populated and never moves backwards.
        var after = await sutRepository.GetByIdAsync(device.Id);
        Assert.Equal("2026.5.1", after!.ClientVersion);
        Assert.NotNull(after.LastActivityDate);
        Assert.True(after.LastActivityDate >= ladBefore);
    }

    /// <summary>
    /// Verifies that a version downgrade is accepted on <c>ClientVersion</c> — unlike
    /// <c>LastActivityDate</c>, there is no forward-only guard on version (users can legitimately
    /// revert installs). <c>LastActivityDate</c> is unchanged because the day-level guard
    /// evaluates false.
    /// </summary>
    [DatabaseTheory]
    [DatabaseData]
    public async Task UpdateLastActivityByIdAsync_VersionDowngrade_AcceptsDowngrade(
        IDeviceRepository sutRepository,
        IUserRepository userRepository)
    {
        // Arrange — stored version is newer than supplied. Downgrades are valid; the column should
        // update (no forward-only guard, unlike LastActivityDate).
        var user = await CreateTestUserAsync(userRepository);
        var device = await CreateTestDeviceAsync(sutRepository, user.Id, clientVersion: "2026.5.1");
        var ladBefore = (await sutRepository.GetByIdAsync(device.Id)).LastActivityDate;

        // Act
        await sutRepository.UpdateLastActivityByIdAsync(device.Id, "2026.4.0");

        // Assert
        var after = await sutRepository.GetByIdAsync(device.Id);
        Assert.Equal("2026.4.0", after!.ClientVersion);
        Assert.Equal(ladBefore, after.LastActivityDate);
    }

    // -------------------------------------------------------------------------------------------
    // UpdateLastActivityByIdentifierAndUserIdAsync
    // -------------------------------------------------------------------------------------------

    /// <summary>
    /// Structurally identical SP body to <c>UpdateLastActivityByIdAsync</c>; only the row-lookup
    /// predicate differs. The tests in this section lock in per-column behavior through that path
    /// so the two SP bodies can't silently drift apart.
    /// </summary>
    [DatabaseTheory]
    [DatabaseData]
    public async Task UpdateLastActivityByIdentifierAndUserIdAsync_VersionChanged_UpdatesClientVersion(
        IDeviceRepository sutRepository,
        IUserRepository userRepository)
    {
        // Arrange
        var user = await CreateTestUserAsync(userRepository);
        var device = await CreateTestDeviceAsync(sutRepository, user.Id, clientVersion: "2026.4.0");
        var ladBefore = (await sutRepository.GetByIdAsync(device.Id)).LastActivityDate;

        // Act
        await sutRepository.UpdateLastActivityByIdentifierAndUserIdAsync(device.Identifier, user.Id, "2026.5.1");

        // Assert — ClientVersion updated; LastActivityDate unchanged (already today).
        var after = await sutRepository.GetByIdAsync(device.Id);
        Assert.Equal("2026.5.1", after!.ClientVersion);
        Assert.Equal(ladBefore, after.LastActivityDate);
    }

    [DatabaseTheory]
    [DatabaseData]
    public async Task UpdateLastActivityByIdentifierAndUserIdAsync_VersionNull_LeavesClientVersionAlone(
        IDeviceRepository sutRepository,
        IUserRepository userRepository)
    {
        // Arrange
        var user = await CreateTestUserAsync(userRepository);
        var device = await CreateTestDeviceAsync(sutRepository, user.Id, clientVersion: "2026.5.1");
        var ladBefore = (await sutRepository.GetByIdAsync(device.Id)).LastActivityDate;

        // Act
        await sutRepository.UpdateLastActivityByIdentifierAndUserIdAsync(device.Identifier, user.Id, clientVersion: null);

        // Assert — both columns preserved (ClientVersion by NULL guard, LAD by day-level guard).
        var after = await sutRepository.GetByIdAsync(device.Id);
        Assert.Equal("2026.5.1", after!.ClientVersion);
        Assert.Equal(ladBefore, after.LastActivityDate);
    }

    [DatabaseTheory]
    [DatabaseData]
    public async Task UpdateLastActivityByIdentifierAndUserIdAsync_VersionDowngrade_AcceptsDowngrade(
        IDeviceRepository sutRepository,
        IUserRepository userRepository)
    {
        // Arrange — stored version is newer than supplied. Downgrades are valid via this path too.
        var user = await CreateTestUserAsync(userRepository);
        var device = await CreateTestDeviceAsync(sutRepository, user.Id, clientVersion: "2026.5.1");
        var ladBefore = (await sutRepository.GetByIdAsync(device.Id)).LastActivityDate;

        // Act
        await sutRepository.UpdateLastActivityByIdentifierAndUserIdAsync(device.Identifier, user.Id, "2026.4.0");

        // Assert — downgrade accepted; LAD unchanged (already today).
        var after = await sutRepository.GetByIdAsync(device.Id);
        Assert.Equal("2026.4.0", after!.ClientVersion);
        Assert.Equal(ladBefore, after.LastActivityDate);
    }

    // -------------------------------------------------------------------------------------------
    // CreateAsync
    // -------------------------------------------------------------------------------------------

    /// <summary>
    /// Locks in that <c>Device_Create</c> persists the last-activity fields (<c>LastActivityDate</c>
    /// and <c>ClientVersion</c>) supplied via the entity. <c>LastActivityDate</c> is set by the
    /// entity initializer (<c>= DateTime.UtcNow</c>); <c>ClientVersion</c> is whatever the caller
    /// supplies. Implicit coverage exists via
    /// <see cref="GetManyByUserIdWithDeviceAuth_ReturnsLastActivityDate_ForNewDeviceAsync"/>
    /// (different read SP); these two tests verify the round-trip via <c>Device_ReadById</c>.
    /// </summary>
    [DatabaseTheory]
    [DatabaseData]
    public async Task CreateAsync_WithClientVersion_PersistsLastActivityFields(
        IDeviceRepository sutRepository,
        IUserRepository userRepository)
    {
        // Arrange
        var user = await CreateTestUserAsync(userRepository);

        // Act
        var device = await CreateTestDeviceAsync(sutRepository, user.Id, clientVersion: "2026.5.1");

        // Assert — re-read the row and confirm both last-activity columns were persisted
        var after = await sutRepository.GetByIdAsync(device.Id);
        Assert.Equal("2026.5.1", after!.ClientVersion);
        Assert.NotNull(after.LastActivityDate);
    }

    [DatabaseTheory]
    [DatabaseData]
    public async Task CreateAsync_WithoutClientVersion_PersistsLastActivityFields(
        IDeviceRepository sutRepository,
        IUserRepository userRepository)
    {
        // Arrange
        var user = await CreateTestUserAsync(userRepository);

        // Act — no ClientVersion supplied (header was absent)
        var device = await CreateTestDeviceAsync(sutRepository, user.Id);

        // Assert — ClientVersion is null (nothing supplied); LastActivityDate is still set via the
        // entity initializer and persisted by Device_Create's @LastActivityDate parameter.
        var after = await sutRepository.GetByIdAsync(device.Id);
        Assert.Null(after!.ClientVersion);
        Assert.NotNull(after.LastActivityDate);
    }
}
