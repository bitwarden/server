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
        });
    }

    // -------------------------------------------------------------------------------------------
    // ReplaceAsync
    // -------------------------------------------------------------------------------------------

    /// <summary>
    /// Verifies the Device_Update NULL-passthrough guards: if a general save (ReplaceAsync) passes
    /// NULL for either bumped column (LastActivityDate or ClientVersion), the stored value must
    /// be preserved. This covers both columns' guards in a single arrange/act/assert so a regression
    /// on either guard fails this test loudly.
    /// </summary>
    [DatabaseTheory]
    [DatabaseData]
    public async Task ReplaceAsync_WithNullBumpedFields_PreservesExistingValues(
        IDeviceRepository sutRepository,
        IUserRepository userRepository)
    {
        // Arrange — device with a stored ClientVersion, then bump to populate LastActivityDate too.
        var user = await CreateTestUserAsync(userRepository);
        var device = await CreateTestDeviceAsync(sutRepository, user.Id, clientVersion: "2026.5.1");

        await sutRepository.BumpDeviceDataByIdAsync(device.Id, clientVersion: null);
        var afterBump = await sutRepository.GetByIdAsync(device.Id);
        Assert.NotNull(afterBump!.LastActivityDate);
        Assert.Equal("2026.5.1", afterBump.ClientVersion);

        // Act — null out BOTH bumped fields and ReplaceAsync; SP-side ISNULL / CASE guards (and the
        // EF-side IsModified=false in ReplaceAsync override) should preserve the stored values.
        afterBump.LastActivityDate = null;
        afterBump.ClientVersion = null;
        await sutRepository.ReplaceAsync(afterBump);

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

        await sutRepository.BumpDeviceDataByIdAsync(device.Id, clientVersion: null);
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

    /// <summary>
    /// Bumping a freshly-created device via the by-identifier path with no <c>ClientVersion</c>
    /// supplied at create or at bump is a same-day no-op on both columns: the SP's day-level
    /// guard fires for <c>LastActivityDate</c> (already today via the entity initializer) and the
    /// NULL guard fires for <c>ClientVersion</c>. Locks in that the bump path does not silently
    /// regress either column when both are already in their expected post-bump state.
    /// </summary>
    [DatabaseTheory]
    [DatabaseData]
    public async Task BumpDeviceDataByIdentifierAndUserIdAsync_OnFreshDeviceWithoutClientVersion_PreservesBumpedColumns(
        IDeviceRepository sutRepository,
        IUserRepository userRepository)
    {
        // Arrange
        var user = await CreateTestUserAsync(userRepository);
        var device = await CreateTestDeviceAsync(sutRepository, user.Id);
        var ladBefore = (await sutRepository.GetByIdAsync(device.Id)).LastActivityDate;

        // Act — bump without a ClientVersion on a same-day fresh device
        await sutRepository.BumpDeviceDataByIdentifierAndUserIdAsync(device.Identifier, user.Id, clientVersion: null);

        // Assert — both columns preserved (LAD same-day; ClientVersion still null)
        var after = await sutRepository.GetByIdAsync(device.Id);
        Assert.Equal(ladBefore, after.LastActivityDate);
        Assert.Null(after.ClientVersion);
    }

    [DatabaseTheory]
    [DatabaseData]
    public async Task BumpDeviceDataByIdentifierAndUserIdAsync_DoesNotBumpOtherUsersDevice(
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

        // Act — bump only userA's device
        await sutRepository.BumpDeviceDataByIdentifierAndUserIdAsync(sharedIdentifier, userA.Id, clientVersion: null);

        // Assert — userA's device is bumped, userB's is unchanged
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
    /// and matches the value set by BumpDeviceDataByIdAsync.
    /// </summary>
    [DatabaseTheory]
    [DatabaseData]
    public async Task GetManyByUserIdWithDeviceAuth_ReturnsLastActivityDate_WhenBumped(
        IDeviceRepository sutRepository,
        IUserRepository userRepository)
    {
        // Arrange
        var user = await CreateTestUserAsync(userRepository);
        var device = await CreateTestDeviceAsync(sutRepository, user.Id);

        await sutRepository.BumpDeviceDataByIdAsync(device.Id, clientVersion: null);
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
        var user = await CreateTestUserAsync(userRepository);
        var beforeCreation = DateTime.UtcNow;
        await CreateTestDeviceAsync(sutRepository, user.Id);

        // Act
        var response = await sutRepository.GetManyByUserIdWithDeviceAuth(user.Id);
        var result = response.Single();

        // Assert — LastActivityDate is set at creation time (>= beforeCreation) and returned by the
        // stored procedure. The >= check locks in that the entity initializer's DateTime.UtcNow
        // flowed through Device_Create rather than the column being set by some default later.
        Assert.NotNull(result.LastActivityDate);
        Assert.True(result.LastActivityDate >= beforeCreation);
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
    // BumpDeviceDataByIdAsync
    // -------------------------------------------------------------------------------------------

    /// <summary>
    /// Bumping with a different <c>ClientVersion</c> updates only that column when the day-level
    /// guard is otherwise satisfied. Asserts both columns to catch cross-column regressions — the
    /// SP/EF query writes both axes via a composite WHERE, so asserting one in isolation could miss
    /// interaction bugs. <c>LastActivityDate</c> is unchanged here because <c>CreateAsync</c> sets
    /// it to <c>DateTime.UtcNow</c> via the entity initializer, so a same-day bump's day-level
    /// guard already evaluates false.
    /// </summary>
    [DatabaseTheory]
    [DatabaseData]
    public async Task BumpDeviceDataByIdAsync_VersionChanged_UpdatesClientVersion(
        IDeviceRepository sutRepository,
        IUserRepository userRepository)
    {
        // Arrange
        var user = await CreateTestUserAsync(userRepository);
        var device = await CreateTestDeviceAsync(sutRepository, user.Id, clientVersion: "2026.4.0");
        var ladBefore = (await sutRepository.GetByIdAsync(device.Id)).LastActivityDate;

        // Act
        await sutRepository.BumpDeviceDataByIdAsync(device.Id, "2026.5.1");

        // Assert
        var after = await sutRepository.GetByIdAsync(device.Id);
        Assert.Equal("2026.5.1", after!.ClientVersion);
        Assert.Equal(ladBefore, after.LastActivityDate);
    }

    /// <summary>
    /// Bumping with the same <c>ClientVersion</c> on a device whose <c>LastActivityDate</c> is
    /// already today is a no-op: the composite WHERE evaluates false on both axes. Locks in that
    /// neither column is touched when nothing needs to change.
    /// </summary>
    [DatabaseTheory]
    [DatabaseData]
    public async Task BumpDeviceDataByIdAsync_VersionUnchanged_DoesNotUpdate(
        IDeviceRepository sutRepository,
        IUserRepository userRepository)
    {
        // Arrange — set the device to today's LastActivityDate AND a fixed ClientVersion. Then bumping
        // with the same version should be a no-op (composite WHERE evaluates false).
        var user = await CreateTestUserAsync(userRepository);
        var device = await CreateTestDeviceAsync(sutRepository, user.Id, clientVersion: "2026.5.1");

        // Bump once with the same version to force LastActivityDate to "today" (so the day-level
        // guard also returns false on the second bump).
        await sutRepository.BumpDeviceDataByIdAsync(device.Id, "2026.5.1");
        var afterFirstBump = await sutRepository.GetByIdAsync(device.Id);
        var lastActivityAfterFirstBump = afterFirstBump!.LastActivityDate;

        // Act — bumping again with the same version should be a no-op
        await sutRepository.BumpDeviceDataByIdAsync(device.Id, "2026.5.1");

        // Assert — neither column changed
        var afterSecondBump = await sutRepository.GetByIdAsync(device.Id);
        Assert.Equal("2026.5.1", afterSecondBump!.ClientVersion);
        Assert.Equal(lastActivityAfterFirstBump, afterSecondBump.LastActivityDate);
    }

    /// <summary>
    /// Bumping with a null <c>ClientVersion</c> (e.g. client missing the header) must not clobber
    /// a stored value — the per-column NULL guard preserves it. <c>LastActivityDate</c> is also
    /// unchanged here because the day-level guard already evaluates false (LAD is today from
    /// <c>CreateAsync</c>).
    /// </summary>
    [DatabaseTheory]
    [DatabaseData]
    public async Task BumpDeviceDataByIdAsync_VersionNull_LeavesClientVersionAlone(
        IDeviceRepository sutRepository,
        IUserRepository userRepository)
    {
        // Arrange
        var user = await CreateTestUserAsync(userRepository);
        var device = await CreateTestDeviceAsync(sutRepository, user.Id, clientVersion: "2026.5.1");
        var ladBefore = (await sutRepository.GetByIdAsync(device.Id)).LastActivityDate;

        // Act — bump with a null version (e.g. client missing the header)
        await sutRepository.BumpDeviceDataByIdAsync(device.Id, clientVersion: null);

        // Assert
        var after = await sutRepository.GetByIdAsync(device.Id);
        Assert.Equal("2026.5.1", after!.ClientVersion);
        Assert.Equal(ladBefore, after.LastActivityDate);
    }

    /// <summary>
    /// Verifies that a stale-version bump updates <c>ClientVersion</c> and does not regress
    /// <c>LastActivityDate</c> (LAD never moves backwards as a side effect of a version-only
    /// update). LAD is "today" here because <c>CreateAsync</c>'s entity initializer set it; the
    /// SP's day-level guard means LAD itself isn't bumped in this scenario, only CV is.
    /// </summary>
    [DatabaseTheory]
    [DatabaseData]
    public async Task BumpDeviceDataByIdAsync_StaleVersion_UpdatesVersionWithoutRegressingLastActivityDate(
        IDeviceRepository sutRepository,
        IUserRepository userRepository)
    {
        // Arrange — device with an old ClientVersion. (LastActivityDate is set to "now" by the
        // entity initializer at CreateAsync; we capture the pre-bump value to confirm the per-column
        // guard correctly evaluates the day boundary on the bump call.)
        var user = await CreateTestUserAsync(userRepository);
        var device = await CreateTestDeviceAsync(sutRepository, user.Id, clientVersion: "2026.4.0");
        var ladBefore = (await sutRepository.GetByIdAsync(device.Id)).LastActivityDate;

        // Act
        await sutRepository.BumpDeviceDataByIdAsync(device.Id, "2026.5.1");

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
    public async Task BumpDeviceDataByIdAsync_VersionDowngrade_AcceptsDowngrade(
        IDeviceRepository sutRepository,
        IUserRepository userRepository)
    {
        // Arrange — stored version is newer than supplied. Downgrades are valid; the column should
        // update (no forward-only guard, unlike LastActivityDate).
        var user = await CreateTestUserAsync(userRepository);
        var device = await CreateTestDeviceAsync(sutRepository, user.Id, clientVersion: "2026.5.1");
        var ladBefore = (await sutRepository.GetByIdAsync(device.Id)).LastActivityDate;

        // Act
        await sutRepository.BumpDeviceDataByIdAsync(device.Id, "2026.4.0");

        // Assert
        var after = await sutRepository.GetByIdAsync(device.Id);
        Assert.Equal("2026.4.0", after!.ClientVersion);
        Assert.Equal(ladBefore, after.LastActivityDate);
    }

    // -------------------------------------------------------------------------------------------
    // BumpDeviceDataByIdentifierAndUserIdAsync
    // -------------------------------------------------------------------------------------------

    /// <summary>
    /// Structurally identical SP body to <c>BumpDeviceDataByIdAsync</c>; only the row-lookup
    /// predicate differs. The tests in this section lock in per-column behavior through that path
    /// so the two SP bodies can't silently drift apart.
    /// </summary>
    [DatabaseTheory]
    [DatabaseData]
    public async Task BumpDeviceDataByIdentifierAndUserIdAsync_VersionChanged_UpdatesClientVersion(
        IDeviceRepository sutRepository,
        IUserRepository userRepository)
    {
        // Arrange
        var user = await CreateTestUserAsync(userRepository);
        var device = await CreateTestDeviceAsync(sutRepository, user.Id, clientVersion: "2026.4.0");
        var ladBefore = (await sutRepository.GetByIdAsync(device.Id)).LastActivityDate;

        // Act
        await sutRepository.BumpDeviceDataByIdentifierAndUserIdAsync(device.Identifier, user.Id, "2026.5.1");

        // Assert — ClientVersion updated; LastActivityDate unchanged (already today).
        var after = await sutRepository.GetByIdAsync(device.Id);
        Assert.Equal("2026.5.1", after!.ClientVersion);
        Assert.Equal(ladBefore, after.LastActivityDate);
    }

    [DatabaseTheory]
    [DatabaseData]
    public async Task BumpDeviceDataByIdentifierAndUserIdAsync_VersionNull_LeavesClientVersionAlone(
        IDeviceRepository sutRepository,
        IUserRepository userRepository)
    {
        // Arrange
        var user = await CreateTestUserAsync(userRepository);
        var device = await CreateTestDeviceAsync(sutRepository, user.Id, clientVersion: "2026.5.1");
        var ladBefore = (await sutRepository.GetByIdAsync(device.Id)).LastActivityDate;

        // Act
        await sutRepository.BumpDeviceDataByIdentifierAndUserIdAsync(device.Identifier, user.Id, clientVersion: null);

        // Assert — both columns preserved (ClientVersion by NULL guard, LAD by day-level guard).
        var after = await sutRepository.GetByIdAsync(device.Id);
        Assert.Equal("2026.5.1", after!.ClientVersion);
        Assert.Equal(ladBefore, after.LastActivityDate);
    }

    [DatabaseTheory]
    [DatabaseData]
    public async Task BumpDeviceDataByIdentifierAndUserIdAsync_VersionDowngrade_AcceptsDowngrade(
        IDeviceRepository sutRepository,
        IUserRepository userRepository)
    {
        // Arrange — stored version is newer than supplied. Downgrades are valid via this path too.
        var user = await CreateTestUserAsync(userRepository);
        var device = await CreateTestDeviceAsync(sutRepository, user.Id, clientVersion: "2026.5.1");
        var ladBefore = (await sutRepository.GetByIdAsync(device.Id)).LastActivityDate;

        // Act
        await sutRepository.BumpDeviceDataByIdentifierAndUserIdAsync(device.Identifier, user.Id, "2026.4.0");

        // Assert — downgrade accepted; LAD unchanged (already today).
        var after = await sutRepository.GetByIdAsync(device.Id);
        Assert.Equal("2026.4.0", after!.ClientVersion);
        Assert.Equal(ladBefore, after.LastActivityDate);
    }

    // -------------------------------------------------------------------------------------------
    // CreateAsync
    // -------------------------------------------------------------------------------------------

    /// <summary>
    /// Locks in that <c>Device_Create</c> persists the bumped fields (<c>LastActivityDate</c> and
    /// <c>ClientVersion</c>) supplied via the entity. <c>LastActivityDate</c> is set by the entity
    /// initializer (<c>= DateTime.UtcNow</c>); <c>ClientVersion</c> is whatever the caller supplies.
    /// Implicit coverage exists via <see cref="GetManyByUserIdWithDeviceAuth_ReturnsLastActivityDate_ForNewDeviceAsync"/>
    /// (different read SP); these two tests verify the round-trip via <c>Device_ReadById</c>.
    /// </summary>
    [DatabaseTheory]
    [DatabaseData]
    public async Task CreateAsync_WithClientVersion_PersistsBumpedFields(
        IDeviceRepository sutRepository,
        IUserRepository userRepository)
    {
        // Arrange
        var user = await CreateTestUserAsync(userRepository);

        // Act
        var device = await CreateTestDeviceAsync(sutRepository, user.Id, clientVersion: "2026.5.1");

        // Assert — re-read the row and confirm both bumped columns were persisted
        var after = await sutRepository.GetByIdAsync(device.Id);
        Assert.Equal("2026.5.1", after!.ClientVersion);
        Assert.NotNull(after.LastActivityDate);
    }

    [DatabaseTheory]
    [DatabaseData]
    public async Task CreateAsync_WithoutClientVersion_PersistsBumpedFields(
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
