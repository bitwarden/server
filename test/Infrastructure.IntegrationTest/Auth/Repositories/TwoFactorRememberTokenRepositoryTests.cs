using AutoMapper;
using Bit.Core.Auth.Entities;
using Bit.Core.Auth.Repositories;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using Bit.Core.Utilities;
using Bit.Infrastructure.EntityFramework.Repositories;
using Bit.Infrastructure.IntegrationTest.AdminConsole;
using DapperTwoFactorRememberTokenRepository = Bit.Infrastructure.Dapper.Auth.Repositories.TwoFactorRememberTokenRepository;
using EfTwoFactorRememberToken = Bit.Infrastructure.EntityFramework.Auth.Models.TwoFactorRememberToken;
using EfTwoFactorRememberTokenRepository = Bit.Infrastructure.EntityFramework.Auth.Repositories.TwoFactorRememberTokenRepository;
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
    // Upsert semantics
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
    // Concurrent upsert
    // -------------------------------------------------------------------------------------------

    /// <summary>
    /// Two logins requesting "remember me" for the same device at the same instant must not produce a
    /// unique-key violation, which would surface to the user as a failed login.
    /// </summary>
    /// <remarks>
    /// Whether the two calls actually collide is timing-dependent, so this is a regression guard on the
    /// common path. The two tests that follow force each repository's fallbacks deterministically.
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

    /// <summary>
    /// A repository whose first read returns a canned value, standing in for a read that was stale by
    /// the time the write happened.
    /// </summary>
    private sealed class StaleFirstReadDapperRepository(string connectionString, TwoFactorRememberToken? staleRow)
        : DapperTwoFactorRememberTokenRepository(connectionString, connectionString)
    {
        private bool _served;

        public override Task<TwoFactorRememberToken?> GetByUserIdDeviceIdAsync(Guid userId, Guid deviceId)
        {
            if (_served)
            {
                return base.GetByUserIdDeviceIdAsync(userId, deviceId);
            }

            _served = true;
            return Task.FromResult(staleRow);
        }
    }

    /// <inheritdoc cref="StaleFirstReadDapperRepository" />
    private sealed class StaleFirstReadEfRepository(
        IServiceScopeFactory serviceScopeFactory, IMapper mapper, TwoFactorRememberToken? staleRow)
        : EfTwoFactorRememberTokenRepository(serviceScopeFactory, mapper)
    {
        private bool _served;

        public override Task<TwoFactorRememberToken?> GetByUserIdDeviceIdAsync(Guid userId, Guid deviceId)
        {
            if (_served)
            {
                return base.GetByUserIdDeviceIdAsync(userId, deviceId);
            }

            _served = true;
            return Task.FromResult(staleRow);
        }
    }

    private static ITwoFactorRememberTokenRepository CreateStaleFirstReadRepository(
        Database database, IServiceProvider services, TwoFactorRememberToken? staleRow) =>
        database.Type == SupportedDatabaseProviders.SqlServer && !database.UseEf
            ? new StaleFirstReadDapperRepository(database.ConnectionString, staleRow)
            : new StaleFirstReadEfRepository(
                services.GetRequiredService<IServiceScopeFactory>(),
                services.GetRequiredService<IMapper>(),
                staleRow);

    /// <summary>
    /// Deletes a token row at the database level, standing in for the expiry sweep.
    /// </summary>
    private static async Task DeleteTokenRowAtDatabaseLevelAsync(
        IServiceProvider services, Database database, Guid id)
    {
        if (database.Type == SupportedDatabaseProviders.SqlServer && !database.UseEf)
        {
            await using var connection = new SqlConnection(database.ConnectionString);
            await connection.OpenAsync(TestContext.Current.CancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText = "DELETE FROM [dbo].[TwoFactorRememberToken] WHERE [Id] = @Id";
            command.Parameters.Add(new SqlParameter("@Id", id));
            await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
            return;
        }

        using var scope = services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DatabaseContext>();
        await dbContext.TwoFactorRememberTokens.Where(r => r.Id == id).ExecuteDeleteAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// The read finds no row, but a concurrent login inserts one before this call's insert runs. The
    /// unique index rejects the insert, and the repository must update the winner's row rather than
    /// fail the login.
    /// </summary>
    [Theory, DatabaseData]
    public async Task UpsertAsync_RowAppearsAfterRead_UpdatesTheWinnersRow(
        Database database,
        IServiceProvider services,
        ITwoFactorRememberTokenRepository sut,
        IUserRepository userRepository,
        IDeviceRepository deviceRepository)
    {
        var user = await userRepository.CreateTestUserAsync();
        var device = await CreateTestDeviceAsync(deviceRepository, user.Id);
        await sut.UpsertAsync(NewToken(user.Id, device.Id, "winner"));
        var winner = await sut.GetByUserIdDeviceIdAsync(user.Id, device.Id);
        Assert.NotNull(winner);

        var loser = CreateStaleFirstReadRepository(database, services, staleRow: null);
        await loser.UpsertAsync(NewToken(user.Id, device.Id, "loser"));

        var row = await sut.GetByUserIdDeviceIdAsync(user.Id, device.Id);
        Assert.NotNull(row);
        Assert.Equal("loser", row.Stamp);
        Assert.Equal(winner.Id, row.Id);
        Assert.Equal(winner.CreationDate, row.CreationDate);
    }

    /// <summary>
    /// The read finds a row, which the expiry sweep then deletes before the update runs. The update
    /// matches nothing, so the repository must create the row instead of returning a token with no
    /// row behind it.
    /// </summary>
    [Theory, DatabaseData]
    public async Task UpsertAsync_RowDeletedAfterRead_CreatesTheRow(
        Database database,
        IServiceProvider services,
        ITwoFactorRememberTokenRepository sut,
        IUserRepository userRepository,
        IDeviceRepository deviceRepository)
    {
        var user = await userRepository.CreateTestUserAsync();
        var device = await CreateTestDeviceAsync(deviceRepository, user.Id);
        await sut.UpsertAsync(NewToken(user.Id, device.Id, "original"));
        var staleRow = await sut.GetByUserIdDeviceIdAsync(user.Id, device.Id);
        Assert.NotNull(staleRow);

        await DeleteTokenRowAtDatabaseLevelAsync(services, database, staleRow.Id);

        var repository = CreateStaleFirstReadRepository(database, services, staleRow);
        await repository.UpsertAsync(NewToken(user.Id, device.Id, "replacement"));

        var row = await sut.GetByUserIdDeviceIdAsync(user.Id, device.Id);
        Assert.NotNull(row);
        Assert.Equal("replacement", row.Stamp);
    }

    // -------------------------------------------------------------------------------------------
    // Rotation
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

        await sut.RotateStampsByUserIdAsync(user.Id, DateTime.UtcNow);

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

        await sut.RotateStampsByUserIdAsync(revokedUser.Id, DateTime.UtcNow);

        var untouched = await sut.GetByUserIdDeviceIdAsync(untouchedUser.Id, untouchedDevice.Id);

        Assert.NotNull(untouched);
        Assert.Equal("stamp-untouched", untouched.Stamp);
    }

    // -------------------------------------------------------------------------------------------
    // Cascade behavior
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
    // Expiry sweep
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
    // The unique index exists on every provider
    // -------------------------------------------------------------------------------------------

    /// <summary>
    /// Proves the one-row-per-device constraint is enforced by the database rather than only by the
    /// upsert's own logic. Inserts directly, since going through <c>UpsertAsync</c> would update.
    /// </summary>
    [Theory, DatabaseData]
    public async Task DuplicateUserIdDeviceId_IsRejectedByUniqueIndex(
        ITwoFactorRememberTokenRepository sut,
        IUserRepository userRepository,
        IDeviceRepository deviceRepository,
        Database database,
        IServiceProvider services)
    {
        var user = await userRepository.CreateTestUserAsync();
        var device = await CreateTestDeviceAsync(deviceRepository, user.Id);
        await sut.UpsertAsync(NewToken(user.Id, device.Id, "stamp-one"));

        await Assert.ThrowsAnyAsync<Exception>(
            () => InsertRowDirectlyAsync(services, database, user.Id, device.Id));
    }

    private static async Task InsertRowDirectlyAsync(
        IServiceProvider services, Database database, Guid userId, Guid deviceId)
    {
        if (database.Type == SupportedDatabaseProviders.SqlServer && !database.UseEf)
        {
            await using var connection = new SqlConnection(database.ConnectionString);
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = """
                INSERT INTO [dbo].[TwoFactorRememberToken]
                    ([Id], [UserId], [DeviceId], [Stamp], [CreationDate], [RevisionDate], [ExpirationDate])
                VALUES
                    (@Id, @UserId, @DeviceId, @Stamp, @Now, @Now, @Expiration)
                """;
            command.Parameters.Add(new SqlParameter("@Id", CoreHelpers.GenerateComb()));
            command.Parameters.Add(new SqlParameter("@UserId", userId));
            command.Parameters.Add(new SqlParameter("@DeviceId", deviceId));
            command.Parameters.Add(new SqlParameter("@Stamp", "stamp-two"));
            command.Parameters.Add(new SqlParameter("@Now", DateTime.UtcNow));
            command.Parameters.Add(new SqlParameter("@Expiration", DateTime.UtcNow.AddDays(30)));
            await command.ExecuteNonQueryAsync();
            return;
        }

        using var scope = services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DatabaseContext>();
        dbContext.TwoFactorRememberTokens.Add(new EfTwoFactorRememberToken
        {
            Id = CoreHelpers.GenerateComb(),
            UserId = userId,
            DeviceId = deviceId,
            Stamp = "stamp-two",
            CreationDate = DateTime.UtcNow,
            RevisionDate = DateTime.UtcNow,
            ExpirationDate = DateTime.UtcNow.AddDays(30),
        });
        await dbContext.SaveChangesAsync();
    }

    // -------------------------------------------------------------------------------------------
    // Storage fidelity
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
