using Bit.Core.Auth.Entities;
using Bit.Core.Auth.Models.Business.Tokenables;
using Bit.Core.Auth.Repositories;
using Bit.Core.Auth.UserFeatures.TwoFactorAuth.Implementations;
using Bit.Core.Entities;
using Bit.Core.Tokens;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.Auth.UserFeatures.TwoFactorAuth;

/// <summary>
/// One test per rejection path. Each is asserted in isolation — a test that broke two checks at once
/// would still pass if one of them were dropped from the implementation.
/// </summary>
/// <remarks>
/// Every time value here comes from one <see cref="FakeTimeProvider"/> frozen at <c>_now</c>. The real clock
/// is never read, so the results do not depend on the day the tests run.
/// </remarks>
[SutProviderCustomize]
public class ValidateTwoFactorRememberTokenQueryTests
{
    private const string _deviceIdentifier = "device-identifier";
    private const string _token = "protected-token";

    /// <summary>The instant the fake clock is frozen at.</summary>
    private static readonly DateTime _now = new(2026, 10, 2, 12, 0, 0, DateTimeKind.Utc);

    /// <summary>
    /// When a valid token and its row lapse. In production the row takes its expiry from the token,
    /// so the two match.
    /// </summary>
    private static readonly DateTime _expiration = _now.AddDays(30);

    /// <summary>
    /// Builds the query with a fake clock frozen at <c>_now</c>. The fake clock moves only when a test
    /// calls <c>SetUtcNow</c> or <c>Advance</c> on it.
    /// </summary>
    private static SutProvider<ValidateTwoFactorRememberTokenQuery> GetSutProvider()
    {
        var sutProvider = new SutProvider<ValidateTwoFactorRememberTokenQuery>()
            .WithFakeTimeProvider()
            .Create();
        sutProvider.GetDependency<FakeTimeProvider>().SetUtcNow(_now);
        return sutProvider;
    }

    private static TwoFactorRememberTokenable Tokenable(User user, Guid deviceId, string stamp, TimeProvider clock) =>
        new()
        {
            UserId = user.Id,
            DeviceId = deviceId,
            DeviceIdentifier = _deviceIdentifier,
            Stamp = stamp,
            SecurityStamp = user.SecurityStamp,
            ExpirationDate = _expiration,
            // A token judges its own expiry with its own clock, which defaults to the system clock. In
            // production a presented token is deserialized and keeps that default. Here the data protector is
            // mocked, so the fake clock is assigned directly to judge the token against the same frozen
            // instant as the row.
            TimeProvider = clock,
        };

    private static TwoFactorRememberToken Row(User user, Guid deviceId, string stamp) =>
        new()
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            DeviceId = deviceId,
            Stamp = stamp,
            ExpirationDate = _expiration,
        };

    /// <summary>
    /// Wires the happy path: token unprotects and the row exists with a matching stamp.
    /// Individual tests then break exactly one of those.
    /// </summary>
    private static (TwoFactorRememberTokenable tokenable, TwoFactorRememberToken row) ArrangeValid(
        SutProvider<ValidateTwoFactorRememberTokenQuery> sutProvider, User user, Guid deviceId)
    {
        const string stamp = "row-stamp";
        var tokenable = Tokenable(user, deviceId, stamp, sutProvider.GetDependency<FakeTimeProvider>());
        var row = Row(user, deviceId, stamp);

        sutProvider.GetDependency<IDataProtectorTokenFactory<TwoFactorRememberTokenable>>()
            .TryUnprotect(_token, out Arg.Any<TwoFactorRememberTokenable>())
            .Returns(c => { c[1] = tokenable; return true; });

        sutProvider.GetDependency<ITwoFactorRememberTokenRepository>()
            .GetByUserIdDeviceIdAsync(user.Id, deviceId)
            .Returns(row);

        return (tokenable, row);
    }

    /// <summary>The happy path.</summary>
    [Theory, BitAutoData]
    public async Task ValidateAsync_EverythingMatches_ReturnsTrue(
        User user, Guid deviceId)
    {
        var sutProvider = GetSutProvider();
        ArrangeValid(sutProvider, user, deviceId);

        Assert.True(await sutProvider.Sut.ValidateAsync(user, _deviceIdentifier, _token));
    }

    /// <summary>The targeted revocation: the device's row stamp has been rotated.</summary>
    [Theory, BitAutoData]
    public async Task ValidateAsync_RowStampRotated_ReturnsFalse(
        User user, Guid deviceId)
    {
        var sutProvider = GetSutProvider();
        var (_, row) = ArrangeValid(sutProvider, user, deviceId);
        row.Stamp = "rotated-stamp";

        Assert.False(await sutProvider.Sut.ValidateAsync(user, _deviceIdentifier, _token));
    }

    /// <summary>
    /// The account-wide check: rotating the user's security stamp invalidates every 2FA remember token they hold.
    /// </summary>
    [Theory, BitAutoData]
    public async Task ValidateAsync_UserSecurityStampRotated_ReturnsFalse(
        User user, Guid deviceId)
    {
        var sutProvider = GetSutProvider();
        var (tokenable, _) = ArrangeValid(sutProvider, user, deviceId);
        tokenable.SecurityStamp = "a-different-security-stamp";

        Assert.False(await sutProvider.Sut.ValidateAsync(user, _deviceIdentifier, _token));
    }

    /// <summary>A token issued to a different user.</summary>
    [Theory, BitAutoData]
    public async Task ValidateAsync_UserIdMismatch_ReturnsFalse(
        User user, Guid deviceId)
    {
        var sutProvider = GetSutProvider();
        var (tokenable, _) = ArrangeValid(sutProvider, user, deviceId);
        tokenable.UserId = Guid.NewGuid();

        Assert.False(await sutProvider.Sut.ValidateAsync(user, _deviceIdentifier, _token));
    }

    /// <summary>The device binding.</summary>
    [Theory, BitAutoData]
    public async Task ValidateAsync_DeviceIdentifierMismatch_ReturnsFalse(
        User user, Guid deviceId)
    {
        var sutProvider = GetSutProvider();
        ArrangeValid(sutProvider, user, deviceId);

        Assert.False(await sutProvider.Sut.ValidateAsync(user, "a-different-device", _token));
    }

    /// <summary>
    /// Identifiers are matched the way SQL collation matches them, so a case difference must
    /// not cause a spurious challenge.
    /// </summary>
    [Theory, BitAutoData]
    public async Task ValidateAsync_DeviceIdentifierDiffersOnlyByCase_ReturnsTrue(
        User user, Guid deviceId)
    {
        var sutProvider = GetSutProvider();
        ArrangeValid(sutProvider, user, deviceId);

        Assert.True(await sutProvider.Sut.ValidateAsync(user, _deviceIdentifier.ToUpperInvariant(), _token));
    }

    /// <summary>No row for this device.</summary>
    [Theory, BitAutoData]
    public async Task ValidateAsync_RowMissing_ReturnsFalse(
        User user, Guid deviceId)
    {
        var sutProvider = GetSutProvider();
        ArrangeValid(sutProvider, user, deviceId);
        sutProvider.GetDependency<ITwoFactorRememberTokenRepository>()
            .GetByUserIdDeviceIdAsync(user.Id, deviceId)
            .Returns((TwoFactorRememberToken?)null);

        Assert.False(await sutProvider.Sut.ValidateAsync(user, _deviceIdentifier, _token));
    }

    /// <summary>The row outlived its expiry but the sweep has not reached it.</summary>
    [Theory, BitAutoData]
    public async Task ValidateAsync_RowExpired_ReturnsFalse(
        User user, Guid deviceId)
    {
        var sutProvider = GetSutProvider();
        var (_, row) = ArrangeValid(sutProvider, user, deviceId);
        row.ExpirationDate = _now.AddMinutes(-1);

        Assert.False(await sutProvider.Sut.ValidateAsync(user, _deviceIdentifier, _token));
    }

    /// <summary>
    /// A row is honored through its <c>ExpirationDate</c> itself and refused from one tick later, the same
    /// boundary <c>ExpiringTokenable.IsExpired</c> applies to the token.
    /// </summary>
    [Theory, BitAutoData]
    public async Task ValidateAsync_RowExpirationEqualsExactlyNow_ReturnsTrue(User user, Guid deviceId)
    {
        var sutProvider = GetSutProvider();
        var (_, row) = ArrangeValid(sutProvider, user, deviceId);
        row.ExpirationDate = _now;

        Assert.True(await sutProvider.Sut.ValidateAsync(user, _deviceIdentifier, _token));
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_RowExpiredOneTickAgo_ReturnsFalse(User user, Guid deviceId)
    {
        var sutProvider = GetSutProvider();
        var (_, row) = ArrangeValid(sutProvider, user, deviceId);
        row.ExpirationDate = _now.AddTicks(-1);

        Assert.False(await sutProvider.Sut.ValidateAsync(user, _deviceIdentifier, _token));
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_TokenCannotBeUnprotected_ReturnsFalse(
        User user)
    {
        var sutProvider = GetSutProvider();
        sutProvider.GetDependency<IDataProtectorTokenFactory<TwoFactorRememberTokenable>>()
            .TryUnprotect(_token, out Arg.Any<TwoFactorRememberTokenable>())
            .Returns(c => { c[1] = null!; return false; });

        Assert.False(await sutProvider.Sut.ValidateAsync(user, _deviceIdentifier, _token));
    }

    /// <summary>The token's own expiry, which is checked before the row is read.</summary>
    [Theory, BitAutoData]
    public async Task ValidateAsync_TokenItselfExpired_ReturnsFalse(
        User user, Guid deviceId)
    {
        var sutProvider = GetSutProvider();
        var (tokenable, _) = ArrangeValid(sutProvider, user, deviceId);
        tokenable.ExpirationDate = _now.AddMinutes(-1);

        Assert.False(await sutProvider.Sut.ValidateAsync(user, _deviceIdentifier, _token));
    }

    /// <summary>
    /// The token is honored through its <c>ExpirationDate</c> itself and refused from one tick later, which is
    /// what <c>ExpiringTokenable.IsExpired</c> enforces and the same boundary the row check applies to the row.
    /// </summary>
    [Theory, BitAutoData]
    public async Task ValidateAsync_TokenExpirationEqualsExactlyNow_ReturnsTrue(User user, Guid deviceId)
    {
        var sutProvider = GetSutProvider();
        var (tokenable, _) = ArrangeValid(sutProvider, user, deviceId);
        tokenable.ExpirationDate = _now;

        Assert.True(await sutProvider.Sut.ValidateAsync(user, _deviceIdentifier, _token));
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_TokenExpiredOneTickAgo_ReturnsFalse(User user, Guid deviceId)
    {
        var sutProvider = GetSutProvider();
        var (tokenable, _) = ArrangeValid(sutProvider, user, deviceId);
        tokenable.ExpirationDate = _now.AddTicks(-1);

        Assert.False(await sutProvider.Sut.ValidateAsync(user, _deviceIdentifier, _token));
    }

    /// <summary>
    /// Moving the one shared clock forward is enough to expire both the token and its row: honored
    /// right up to the expiry instant, refused one tick after it.
    /// </summary>
    [Theory, BitAutoData]
    public async Task ValidateAsync_ClockAdvancesPastExpiration_StopsHonoringToken(User user, Guid deviceId)
    {
        var sutProvider = GetSutProvider();
        ArrangeValid(sutProvider, user, deviceId);
        var clock = sutProvider.GetDependency<FakeTimeProvider>();

        Assert.True(await sutProvider.Sut.ValidateAsync(user, _deviceIdentifier, _token));

        clock.Advance(_expiration - _now);
        Assert.True(await sutProvider.Sut.ValidateAsync(user, _deviceIdentifier, _token));

        clock.Advance(TimeSpan.FromTicks(1));
        Assert.False(await sutProvider.Sut.ValidateAsync(user, _deviceIdentifier, _token));
    }

    /// <summary>Validation reads; it must never write.</summary>
    [Theory, BitAutoData]
    public async Task ValidateAsync_Always_DoesNotWrite(
        User user, Guid deviceId)
    {
        var sutProvider = GetSutProvider();
        ArrangeValid(sutProvider, user, deviceId);

        await sutProvider.Sut.ValidateAsync(user, _deviceIdentifier, _token);

        var repository = sutProvider.GetDependency<ITwoFactorRememberTokenRepository>();
        await repository.DidNotReceiveWithAnyArgs().UpsertAsync(default!);
        await repository.DidNotReceiveWithAnyArgs().RotateStampsByUserIdAsync(default, default);
        await repository.DidNotReceiveWithAnyArgs().DeleteExpiredAsync(default);
    }
}
