using Bit.Core.Auth.Entities;
using Bit.Core.Auth.Models.Business.Tokenables;
using Bit.Core.Auth.Repositories;
using Bit.Core.Auth.UserFeatures.TwoFactorAuth.Implementations;
using Bit.Core.Auth.UserFeatures.TwoFactorAuth.Interfaces;
using Bit.Core.Entities;
using Bit.Core.Tokens;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.Auth.UserFeatures.TwoFactorAuth;

/// <summary>
/// One test per rejection path. Each is asserted in isolation — a test that broke two checks at once
/// would still pass if one of them were dropped from the implementation.
/// </summary>
[SutProviderCustomize]
public class ValidateTwoFactorRememberTokenQueryTests
{
    private const string _deviceIdentifier = "device-identifier";
    private const string _token = "protected-token";

    private static TwoFactorRememberTokenable Tokenable(User user, Guid deviceId, string stamp) =>
        new()
        {
            UserId = user.Id,
            DeviceId = deviceId,
            DeviceIdentifier = _deviceIdentifier,
            Stamp = stamp,
            SecurityStamp = user.SecurityStamp,
        };

    private static TwoFactorRememberToken Row(User user, Guid deviceId, string stamp) =>
        new()
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            DeviceId = deviceId,
            Stamp = stamp,
            ExpirationDate = DateTime.UtcNow.AddDays(1),
        };

    /// <summary>
    /// Wires the happy path: token unprotects, 2FA is on, and the row exists with a matching stamp.
    /// Individual tests then break exactly one of those.
    /// </summary>
    private static (TwoFactorRememberTokenable tokenable, TwoFactorRememberToken row) ArrangeValid(
        SutProvider<ValidateTwoFactorRememberTokenQuery> sutProvider, User user, Guid deviceId)
    {
        const string stamp = "row-stamp";
        var tokenable = Tokenable(user, deviceId, stamp);
        var row = Row(user, deviceId, stamp);

        sutProvider.GetDependency<IDataProtectorTokenFactory<TwoFactorRememberTokenable>>()
            .TryUnprotect(_token, out Arg.Any<TwoFactorRememberTokenable>())
            .Returns(c => { c[1] = tokenable; return true; });

        sutProvider.GetDependency<ITwoFactorIsEnabledQuery>()
            .TwoFactorIsEnabledAsync(user)
            .Returns(true);

        sutProvider.GetDependency<ITwoFactorRememberTokenRepository>()
            .GetByUserIdDeviceIdAsync(user.Id, deviceId)
            .Returns(row);

        return (tokenable, row);
    }

    /// <summary>U4 — the happy path.</summary>
    [Theory, BitAutoData]
    public async Task ValidateAsync_EverythingMatches_ReturnsTrue(
        SutProvider<ValidateTwoFactorRememberTokenQuery> sutProvider, User user, Guid deviceId)
    {
        ArrangeValid(sutProvider, user, deviceId);

        Assert.True(await sutProvider.Sut.ValidateAsync(user, _deviceIdentifier, _token));
    }

    /// <summary>U5 — the targeted revocation: the device's row stamp has been rotated.</summary>
    [Theory, BitAutoData]
    public async Task ValidateAsync_RowStampRotated_ReturnsFalse(
        SutProvider<ValidateTwoFactorRememberTokenQuery> sutProvider, User user, Guid deviceId)
    {
        var (_, row) = ArrangeValid(sutProvider, user, deviceId);
        row.Stamp = "rotated-stamp";

        Assert.False(await sutProvider.Sut.ValidateAsync(user, _deviceIdentifier, _token));
    }

    /// <summary>U6 — the account-wide check inherited from the previous design.</summary>
    [Theory, BitAutoData]
    public async Task ValidateAsync_UserSecurityStampRotated_ReturnsFalse(
        SutProvider<ValidateTwoFactorRememberTokenQuery> sutProvider, User user, Guid deviceId)
    {
        var (tokenable, _) = ArrangeValid(sutProvider, user, deviceId);
        tokenable.SecurityStamp = "a-different-security-stamp";

        Assert.False(await sutProvider.Sut.ValidateAsync(user, _deviceIdentifier, _token));
    }

    /// <summary>
    /// U7 — a remember token cannot stand in as a second factor for an account that currently has no
    /// second factor configured.
    /// </summary>
    [Theory, BitAutoData]
    public async Task ValidateAsync_UserHasNoTwoFactorEnabled_ReturnsFalse(
        SutProvider<ValidateTwoFactorRememberTokenQuery> sutProvider, User user, Guid deviceId)
    {
        ArrangeValid(sutProvider, user, deviceId);
        sutProvider.GetDependency<ITwoFactorIsEnabledQuery>()
            .TwoFactorIsEnabledAsync(user)
            .Returns(false);

        Assert.False(await sutProvider.Sut.ValidateAsync(user, _deviceIdentifier, _token));
    }

    /// <summary>
    /// The 2FA-enabled check is ordered ahead of the row read, so the common teardown case rejects
    /// without touching the table.
    /// </summary>
    [Theory, BitAutoData]
    public async Task ValidateAsync_UserHasNoTwoFactorEnabled_DoesNotReadRow(
        SutProvider<ValidateTwoFactorRememberTokenQuery> sutProvider, User user, Guid deviceId)
    {
        ArrangeValid(sutProvider, user, deviceId);
        sutProvider.GetDependency<ITwoFactorIsEnabledQuery>()
            .TwoFactorIsEnabledAsync(user)
            .Returns(false);

        await sutProvider.Sut.ValidateAsync(user, _deviceIdentifier, _token);

        await sutProvider.GetDependency<ITwoFactorRememberTokenRepository>()
            .DidNotReceiveWithAnyArgs()
            .GetByUserIdDeviceIdAsync(default, default);
    }

    /// <summary>U8 — a token issued to a different user.</summary>
    [Theory, BitAutoData]
    public async Task ValidateAsync_UserIdMismatch_ReturnsFalse(
        SutProvider<ValidateTwoFactorRememberTokenQuery> sutProvider, User user, Guid deviceId)
    {
        var (tokenable, _) = ArrangeValid(sutProvider, user, deviceId);
        tokenable.UserId = Guid.NewGuid();

        Assert.False(await sutProvider.Sut.ValidateAsync(user, _deviceIdentifier, _token));
    }

    /// <summary>U9 — the device binding.</summary>
    [Theory, BitAutoData]
    public async Task ValidateAsync_DeviceIdentifierMismatch_ReturnsFalse(
        SutProvider<ValidateTwoFactorRememberTokenQuery> sutProvider, User user, Guid deviceId)
    {
        ArrangeValid(sutProvider, user, deviceId);

        Assert.False(await sutProvider.Sut.ValidateAsync(user, "a-different-device", _token));
    }

    /// <summary>
    /// U9 — identifiers are matched the way SQL collation matches them, so a case difference must
    /// not cause a spurious challenge.
    /// </summary>
    [Theory, BitAutoData]
    public async Task ValidateAsync_DeviceIdentifierDiffersOnlyByCase_ReturnsTrue(
        SutProvider<ValidateTwoFactorRememberTokenQuery> sutProvider, User user, Guid deviceId)
    {
        ArrangeValid(sutProvider, user, deviceId);

        Assert.True(await sutProvider.Sut.ValidateAsync(user, _deviceIdentifier.ToUpperInvariant(), _token));
    }

    /// <summary>U10 — no row for this device.</summary>
    [Theory, BitAutoData]
    public async Task ValidateAsync_RowMissing_ReturnsFalse(
        SutProvider<ValidateTwoFactorRememberTokenQuery> sutProvider, User user, Guid deviceId)
    {
        ArrangeValid(sutProvider, user, deviceId);
        sutProvider.GetDependency<ITwoFactorRememberTokenRepository>()
            .GetByUserIdDeviceIdAsync(user.Id, deviceId)
            .Returns((TwoFactorRememberToken?)null);

        Assert.False(await sutProvider.Sut.ValidateAsync(user, _deviceIdentifier, _token));
    }

    /// <summary>U11 — the row outlived its expiry but the sweep has not reached it.</summary>
    [Theory, BitAutoData]
    public async Task ValidateAsync_RowExpired_ReturnsFalse(
        SutProvider<ValidateTwoFactorRememberTokenQuery> sutProvider, User user, Guid deviceId)
    {
        var (_, row) = ArrangeValid(sutProvider, user, deviceId);
        row.ExpirationDate = DateTime.UtcNow.AddMinutes(-1);

        Assert.False(await sutProvider.Sut.ValidateAsync(user, _deviceIdentifier, _token));
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_TokenCannotBeUnprotected_ReturnsFalse(
        SutProvider<ValidateTwoFactorRememberTokenQuery> sutProvider, User user)
    {
        sutProvider.GetDependency<IDataProtectorTokenFactory<TwoFactorRememberTokenable>>()
            .TryUnprotect(_token, out Arg.Any<TwoFactorRememberTokenable>())
            .Returns(c => { c[1] = null!; return false; });

        Assert.False(await sutProvider.Sut.ValidateAsync(user, _deviceIdentifier, _token));
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_TokenItselfExpired_ReturnsFalse(
        SutProvider<ValidateTwoFactorRememberTokenQuery> sutProvider, User user, Guid deviceId)
    {
        var (tokenable, _) = ArrangeValid(sutProvider, user, deviceId);
        tokenable.ExpirationDate = DateTime.UtcNow.AddMinutes(-1);

        Assert.False(await sutProvider.Sut.ValidateAsync(user, _deviceIdentifier, _token));
    }

    /// <summary>Validation reads; it must never write.</summary>
    [Theory, BitAutoData]
    public async Task ValidateAsync_Always_DoesNotWrite(
        SutProvider<ValidateTwoFactorRememberTokenQuery> sutProvider, User user, Guid deviceId)
    {
        ArrangeValid(sutProvider, user, deviceId);

        await sutProvider.Sut.ValidateAsync(user, _deviceIdentifier, _token);

        var repository = sutProvider.GetDependency<ITwoFactorRememberTokenRepository>();
        await repository.DidNotReceiveWithAnyArgs().UpsertAsync(default!);
        await repository.DidNotReceiveWithAnyArgs().RotateStampsByUserIdAsync(default);
        await repository.DidNotReceiveWithAnyArgs().DeleteExpiredAsync(default);
    }
}
