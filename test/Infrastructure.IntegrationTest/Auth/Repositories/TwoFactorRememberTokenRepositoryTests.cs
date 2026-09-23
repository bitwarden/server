using Bit.Core.Auth.Entities;
using Bit.Core.Auth.Repositories;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using Bit.Infrastructure.EntityFramework.Repositories;
using Bit.Infrastructure.IntegrationTest.AdminConsole;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Bit.Infrastructure.IntegrationTest.Auth.Repositories;

public class TwoFactorRememberTokenRepositoryTests
{
    // -------------------------------------------------------------------------------------------
    // Test helpers
    // -------------------------------------------------------------------------------------------

    private static Task<Device> CreateTestDeviceAsync(IDeviceRepository deviceRepository, Guid userId) =>
        deviceRepository.CreateAsync(new Device
        {
            UserId = userId,
            Name = "chrome-test",
            Type = DeviceType.ChromeBrowser,
            Identifier = Guid.NewGuid().ToString(),
            Active = true,
            LastActivityDate = DateTime.UtcNow,
        });

    private static TwoFactorRememberToken NewToken(
        Guid userId,
        Guid deviceId,
        string stamp,
        DateTime? expirationDate = null) =>
        new()
        {
            UserId = userId,
            DeviceId = deviceId,
            Stamp = stamp,
            ExpirationDate = expirationDate ?? DateTime.UtcNow.AddDays(30),
        };

    /// <summary>
    /// Deletes a device row at the database level, bypassing repository code entirely.
    /// </summary>
    /// <remarks>
    /// There is no <c>Device_DeleteById</c> stored procedure, so the Dapper <c>DeviceRepository</c>
    /// cannot delete. More importantly, the claim under test is about a database-level cascade, so
    /// the delete has to reach the database without EF resolving the cascade in memory first —
    /// hence <c>ExecuteDeleteAsync</c> rather than <c>Remove</c> plus <c>SaveChanges</c>.
    /// </remarks>
    private static async Task DeleteDeviceAtDatabaseLevelAsync(
        IServiceProvider services, Database database, Guid deviceId)
    {
        if (database.Type == SupportedDatabaseProviders.SqlServer && !database.UseEf)
        {
            await using var connection = new SqlConnection(database.ConnectionString);
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = "DELETE FROM [dbo].[Device] WHERE [Id] = @Id";
            command.Parameters.Add(new SqlParameter("@Id", deviceId));
            await command.ExecuteNonQueryAsync();
            return;
        }

        using var scope = services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DatabaseContext>();
        await dbContext.Devices.Where(d => d.Id == deviceId).ExecuteDeleteAsync();
    }

    // -------------------------------------------------------------------------------------------
    // R1 — upsert semantics
    // -------------------------------------------------------------------------------------------

    [Theory, DatabaseData]
    public async Task UpsertAsync_SameUserAndDevice_UpdatesInPlaceAndPreservesCreationDate(
        ITwoFactorRememberTokenRepository sut,
        IUserRepository userRepository,
        IDeviceRepository deviceRepository)
    {
        var user = await userRepository.CreateTestUserAsync();
        var device = await CreateTestDeviceAsync(deviceRepository, user.Id);

        await sut.UpsertAsync(NewToken(user.Id, device.Id, "stamp-one"));
        var inserted = await sut.GetByUserIdDeviceIdAsync(user.Id, device.Id);

        Assert.NotNull(inserted);
        Assert.NotEqual(Guid.Empty, inserted.Id);
        Assert.Equal("stamp-one", inserted.Stamp);

        await sut.UpsertAsync(NewToken(user.Id, device.Id, "stamp-two"));
        var updated = await sut.GetByUserIdDeviceIdAsync(user.Id, device.Id);

        Assert.NotNull(updated);
        Assert.Equal("stamp-two", updated.Stamp);
        // Same row rather than a second one, and the row remembers when the device was first trusted.
        Assert.Equal(inserted.Id, updated.Id);
        Assert.Equal(inserted.CreationDate, updated.CreationDate);
        Assert.True(updated.RevisionDate >= inserted.RevisionDate);
    }

    // -------------------------------------------------------------------------------------------
    // R2 — concurrent upsert
    // -------------------------------------------------------------------------------------------

    /// <summary>
    /// Two logins requesting "remember me" for the same device at the same instant must not produce a
    /// unique-key violation, which would surface to the user as a failed login.
    /// </summary>
    /// <remarks>
    /// On SQL Server this exercises the <c>UPDLOCK, HOLDLOCK</c> range lock inside an explicit
    /// transaction. That hint is MSSQL-only, so on the EF providers this is a regression guard on the
    /// common path rather than proof the lock works.
    /// </remarks>
    [Theory, DatabaseData]
    public async Task UpsertAsync_Concurrent_BothCompleteAndOneRowSurvives(
        ITwoFactorRememberTokenRepository sut,
        IUserRepository userRepository,
        IDeviceRepository deviceRepository)
    {
        var user = await userRepository.CreateTestUserAsync();
        var device = await CreateTestDeviceAsync(deviceRepository, user.Id);

        await Task.WhenAll(
            sut.UpsertAsync(NewToken(user.Id, device.Id, "stamp-a")),
            sut.UpsertAsync(NewToken(user.Id, device.Id, "stamp-b")));

        var row = await sut.GetByUserIdDeviceIdAsync(user.Id, device.Id);

        Assert.NotNull(row);
        Assert.Contains(row.Stamp, new[] { "stamp-a", "stamp-b" });
    }

    // -------------------------------------------------------------------------------------------
    // R3, R4 — rotation
    // -------------------------------------------------------------------------------------------

    [Theory, DatabaseData]
    public async Task RotateStampsByUserIdAsync_RotatesEveryRowForThatUser(
        ITwoFactorRememberTokenRepository sut,
        IUserRepository userRepository,
        IDeviceRepository deviceRepository)
    {
        var user = await userRepository.CreateTestUserAsync();
        var deviceOne = await CreateTestDeviceAsync(deviceRepository, user.Id);
        var deviceTwo = await CreateTestDeviceAsync(deviceRepository, user.Id);

        await sut.UpsertAsync(NewToken(user.Id, deviceOne.Id, "stamp-one"));
        await sut.UpsertAsync(NewToken(user.Id, deviceTwo.Id, "stamp-two"));

        var beforeOne = await sut.GetByUserIdDeviceIdAsync(user.Id, deviceOne.Id);
        var beforeTwo = await sut.GetByUserIdDeviceIdAsync(user.Id, deviceTwo.Id);
        Assert.NotNull(beforeOne);
        Assert.NotNull(beforeTwo);

        await sut.RotateStampsByUserIdAsync(user.Id);

        var afterOne = await sut.GetByUserIdDeviceIdAsync(user.Id, deviceOne.Id);
        var afterTwo = await sut.GetByUserIdDeviceIdAsync(user.Id, deviceTwo.Id);

        Assert.NotNull(afterOne);
        Assert.NotNull(afterTwo);

        // Every device the user remembered is cut off, and the rows are kept rather than deleted.
        Assert.NotEqual(beforeOne.Stamp, afterOne.Stamp);
        Assert.NotEqual(beforeTwo.Stamp, afterTwo.Stamp);
        Assert.Equal(beforeOne.Id, afterOne.Id);
        Assert.Equal(beforeOne.CreationDate, afterOne.CreationDate);
        Assert.True(afterOne.RevisionDate >= beforeOne.RevisionDate);

        // Stamps are generated in C#, never by NEWID(), which would emit upper-case hex.
        Assert.Equal(afterOne.Stamp.ToLowerInvariant(), afterOne.Stamp);
        Assert.Equal(afterTwo.Stamp.ToLowerInvariant(), afterTwo.Stamp);
    }

    [Theory, DatabaseData]
    public async Task RotateStampsByUserIdAsync_LeavesOtherUsersAlone(
        ITwoFactorRememberTokenRepository sut,
        IUserRepository userRepository,
        IDeviceRepository deviceRepository)
    {
        var revokedUser = await userRepository.CreateTestUserAsync("revoked");
        var untouchedUser = await userRepository.CreateTestUserAsync("untouched");
        var revokedDevice = await CreateTestDeviceAsync(deviceRepository, revokedUser.Id);
        var untouchedDevice = await CreateTestDeviceAsync(deviceRepository, untouchedUser.Id);

        await sut.UpsertAsync(NewToken(revokedUser.Id, revokedDevice.Id, "stamp-revoked"));
        await sut.UpsertAsync(NewToken(untouchedUser.Id, untouchedDevice.Id, "stamp-untouched"));

        await sut.RotateStampsByUserIdAsync(revokedUser.Id);

        var untouched = await sut.GetByUserIdDeviceIdAsync(untouchedUser.Id, untouchedDevice.Id);

        Assert.NotNull(untouched);
        Assert.Equal("stamp-untouched", untouched.Stamp);
    }

    // -------------------------------------------------------------------------------------------
    // R5, R6 — cascade behavior
    // -------------------------------------------------------------------------------------------

    [Theory, DatabaseData]
    public async Task DeletingDevice_CascadesToTokenRow(
        ITwoFactorRememberTokenRepository sut,
        IUserRepository userRepository,
        IDeviceRepository deviceRepository,
        Database database,
        IServiceProvider services)
    {
        var user = await userRepository.CreateTestUserAsync();
        var device = await CreateTestDeviceAsync(deviceRepository, user.Id);
        await sut.UpsertAsync(NewToken(user.Id, device.Id, "stamp"));

        Assert.NotNull(await sut.GetByUserIdDeviceIdAsync(user.Id, device.Id));

        await DeleteDeviceAtDatabaseLevelAsync(services, database, device.Id);

        Assert.Null(await sut.GetByUserIdDeviceIdAsync(user.Id, device.Id));
    }

    /// <summary>
    /// Nothing deletes these rows by <c>UserId</c>. They survive a user deletion only because both
    /// user-delete paths delete the user's <c>Device</c> rows first and the cascade follows.
    /// </summary>
    [Theory, DatabaseData]
    public async Task DeleteAsync_User_RemovesTokenRows(
        ITwoFactorRememberTokenRepository sut,
        IUserRepository userRepository,
        IDeviceRepository deviceRepository)
    {
        var user = await userRepository.CreateTestUserAsync();
        var device = await CreateTestDeviceAsync(deviceRepository, user.Id);
        await sut.UpsertAsync(NewToken(user.Id, device.Id, "stamp"));

        await userRepository.DeleteAsync(user);

        Assert.Null(await sut.GetByUserIdDeviceIdAsync(user.Id, device.Id));
    }

    /// <summary>
    /// The bulk path takes different code than <see cref="DeleteAsync_User_RemovesTokenRows"/> on EF —
    /// <c>ExecuteDeleteAsync</c> bypasses change tracking, so it depends on the database-level cascade
    /// actually existing rather than on EF resolving it.
    /// </summary>
    [Theory, DatabaseData]
    public async Task DeleteManyAsync_Users_RemovesTokenRows(
        ITwoFactorRememberTokenRepository sut,
        IUserRepository userRepository,
        IDeviceRepository deviceRepository)
    {
        var user = await userRepository.CreateTestUserAsync();
        var device = await CreateTestDeviceAsync(deviceRepository, user.Id);
        await sut.UpsertAsync(NewToken(user.Id, device.Id, "stamp"));

        await userRepository.DeleteManyAsync(new[] { user });

        Assert.Null(await sut.GetByUserIdDeviceIdAsync(user.Id, device.Id));
    }

    // -------------------------------------------------------------------------------------------
    // R7 — expiry sweep
    // -------------------------------------------------------------------------------------------

    [Theory, DatabaseData]
    public async Task DeleteExpiredAsync_RemovesOnlyExpiredRows(
        ITwoFactorRememberTokenRepository sut,
        IUserRepository userRepository,
        IDeviceRepository deviceRepository)
    {
        var user = await userRepository.CreateTestUserAsync();
        var expiredDevice = await CreateTestDeviceAsync(deviceRepository, user.Id);
        var liveDevice = await CreateTestDeviceAsync(deviceRepository, user.Id);

        // A fixed instant the two rows straddle, so the sweep is deterministic rather than a race
        // against the wall clock.
        var now = DateTime.UtcNow;

        await sut.UpsertAsync(NewToken(user.Id, expiredDevice.Id, "expired", now.AddMinutes(-1)));
        await sut.UpsertAsync(NewToken(user.Id, liveDevice.Id, "live", now.AddMinutes(1)));

        await sut.DeleteExpiredAsync(now);

        Assert.Null(await sut.GetByUserIdDeviceIdAsync(user.Id, expiredDevice.Id));
        Assert.NotNull(await sut.GetByUserIdDeviceIdAsync(user.Id, liveDevice.Id));
    }

    // -------------------------------------------------------------------------------------------
    // R8 — storage fidelity
    // -------------------------------------------------------------------------------------------

    /// <summary>
    /// Cheap guard against a column width or collation mistake on any one provider: the stamp is
    /// compared with ordinal case sensitivity at validation time, so it has to come back byte-identical.
    /// </summary>
    [Theory, DatabaseData]
    public async Task UpsertAsync_StampRoundTripsUnchanged(
        ITwoFactorRememberTokenRepository sut,
        IUserRepository userRepository,
        IDeviceRepository deviceRepository)
    {
        var user = await userRepository.CreateTestUserAsync();
        var device = await CreateTestDeviceAsync(deviceRepository, user.Id);
        var stamp = Guid.NewGuid().ToString();

        await sut.UpsertAsync(NewToken(user.Id, device.Id, stamp));

        var row = await sut.GetByUserIdDeviceIdAsync(user.Id, device.Id);

        Assert.NotNull(row);
        Assert.Equal(stamp, row.Stamp, StringComparer.Ordinal);
        Assert.Equal(user.Id, row.UserId);
        Assert.Equal(device.Id, row.DeviceId);
    }
}
