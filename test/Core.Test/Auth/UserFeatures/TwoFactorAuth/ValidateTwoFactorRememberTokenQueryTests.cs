using Bit.Core.AdminConsole.Entities;
using Bit.Core.Auth.Entities;
using Bit.Core.Auth.Models.Business.Tokenables;
using Bit.Core.Auth.Repositories;
using Bit.Core.Auth.UserFeatures.TwoFactorAuth.Implementations;
using Bit.Core.Auth.UserFeatures.TwoFactorAuth.Interfaces;
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
[SutProviderCustomize]
public class ValidateTwoFactorRememberTokenQueryTests
{
    private const string _deviceIdentifier = "device-identifier";
    private const string _token = "protected-token";
    private const string _organizationDuoProviders =
        """{"6":{"Enabled":true,"MetaData":{"ClientSecret":"s","ClientId":"c","Host":"example.com"}}}""";

    private static readonly DateTime _now = new(2026, 10, 2, 12, 0, 0, DateTimeKind.Utc);

    private static SutProvider<ValidateTwoFactorRememberTokenQuery> GetSutProvider()
    {
        var sutProvider = new SutProvider<ValidateTwoFactorRememberTokenQuery>()
            .WithFakeTimeProvider()
            .Create();
        sutProvider.GetDependency<FakeTimeProvider>().SetUtcNow(_now);
        return sutProvider;
    }

    private static TwoFactorRememberTokenable Tokenable(User user, Guid deviceId, string stamp) =>
        new()
        {
            UserId = user.Id,
            DeviceId = deviceId,
            DeviceIdentifier = _deviceIdentifier,
            Stamp = stamp,
            SecurityStamp = user.SecurityStamp,
            // Real clock: ExpiringTokenable reads DateTime.UtcNow directly.
            ExpirationDate = DateTime.UtcNow.AddDays(30),
        };

    private static TwoFactorRememberToken Row(User user, Guid deviceId, string stamp) =>
        new()
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            DeviceId = deviceId,
            Stamp = stamp,
            ExpirationDate = _now.AddDays(1),
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

    /// <summary>The happy path.</summary>
    [Theory, BitAutoData]
    public async Task ValidateAsync_EverythingMatches_ReturnsTrue(
        User user, Guid deviceId)
    {
        var sutProvider = GetSutProvider();
        ArrangeValid(sutProvider, user, deviceId);

        Assert.True(await sutProvider.Sut.ValidateAsync(user, null, _deviceIdentifier, _token));
    }

    /// <summary>The targeted revocation: the device's row stamp has been rotated.</summary>
    [Theory, BitAutoData]
    public async Task ValidateAsync_RowStampRotated_ReturnsFalse(
        User user, Guid deviceId)
    {
        var sutProvider = GetSutProvider();
        var (_, row) = ArrangeValid(sutProvider, user, deviceId);
        row.Stamp = "rotated-stamp";

        Assert.False(await sutProvider.Sut.ValidateAsync(user, null, _deviceIdentifier, _token));
    }

    /// <summary>The account-wide check inherited from the previous design.</summary>
    [Theory, BitAutoData]
    public async Task ValidateAsync_UserSecurityStampRotated_ReturnsFalse(
        User user, Guid deviceId)
    {
        var sutProvider = GetSutProvider();
        var (tokenable, _) = ArrangeValid(sutProvider, user, deviceId);
        tokenable.SecurityStamp = "a-different-security-stamp";

        Assert.False(await sutProvider.Sut.ValidateAsync(user, null, _deviceIdentifier, _token));
    }

    /// <summary>
    /// A remember token cannot stand in as a second factor for an account that currently has no
    /// second factor configured.
    /// </summary>
    [Theory, BitAutoData]
    public async Task ValidateAsync_UserHasNoTwoFactorEnabled_ReturnsFalse(
        User user, Guid deviceId)
    {
        var sutProvider = GetSutProvider();
        ArrangeValid(sutProvider, user, deviceId);
        sutProvider.GetDependency<ITwoFactorIsEnabledQuery>()
            .TwoFactorIsEnabledAsync(user)
            .Returns(false);

        Assert.False(await sutProvider.Sut.ValidateAsync(user, null, _deviceIdentifier, _token));
    }

    /// <summary>
    /// A user whose only second factor is enforced by their organization is still covered, so their
    /// remember token is honored.
    /// </summary>
    [Theory, BitAutoData]
    public async Task ValidateAsync_OrgEnforcesTwoFactor_NoPersonalTwoFactor_ReturnsTrue(
        User user, Organization organization, Guid deviceId)
    {
        var sutProvider = GetSutProvider();
        ArrangeValid(sutProvider, user, deviceId);
        sutProvider.GetDependency<ITwoFactorIsEnabledQuery>()
            .TwoFactorIsEnabledAsync(user)
            .Returns(false);
        organization.Use2fa = true;
        organization.TwoFactorProviders = _organizationDuoProviders;

        Assert.True(await sutProvider.Sut.ValidateAsync(user, organization, _deviceIdentifier, _token));
    }

    /// <summary>
    /// An organization that does not enforce two-factor gives a user with none of their own no second
    /// factor to stand in for.
    /// </summary>
    [Theory, BitAutoData]
    public async Task ValidateAsync_OrgDoesNotEnforceTwoFactor_NoPersonalTwoFactor_ReturnsFalse(
        User user, Organization organization, Guid deviceId)
    {
        var sutProvider = GetSutProvider();
        ArrangeValid(sutProvider, user, deviceId);
        sutProvider.GetDependency<ITwoFactorIsEnabledQuery>()
            .TwoFactorIsEnabledAsync(user)
            .Returns(false);
        organization.Use2fa = false;
        organization.TwoFactorProviders = _organizationDuoProviders;

        Assert.False(await sutProvider.Sut.ValidateAsync(user, organization, _deviceIdentifier, _token));
    }

    /// <summary>
    /// The 2FA-enabled check is ordered ahead of the row read, so the common teardown case rejects
    /// without touching the table.
    /// </summary>
    [Theory, BitAutoData]
    public async Task ValidateAsync_UserHasNoTwoFactorEnabled_DoesNotReadRow(
        User user, Guid deviceId)
    {
        var sutProvider = GetSutProvider();
        ArrangeValid(sutProvider, user, deviceId);
        sutProvider.GetDependency<ITwoFactorIsEnabledQuery>()
            .TwoFactorIsEnabledAsync(user)
            .Returns(false);

        await sutProvider.Sut.ValidateAsync(user, null, _deviceIdentifier, _token);

        await sutProvider.GetDependency<ITwoFactorRememberTokenRepository>()
            .DidNotReceiveWithAnyArgs()
            .GetByUserIdDeviceIdAsync(default, default);
    }

    /// <summary>A token issued to a different user.</summary>
    [Theory, BitAutoData]
    public async Task ValidateAsync_UserIdMismatch_ReturnsFalse(
        User user, Guid deviceId)
    {
        var sutProvider = GetSutProvider();
        var (tokenable, _) = ArrangeValid(sutProvider, user, deviceId);
        tokenable.UserId = Guid.NewGuid();

        Assert.False(await sutProvider.Sut.ValidateAsync(user, null, _deviceIdentifier, _token));
    }

    /// <summary>The device binding.</summary>
    [Theory, BitAutoData]
    public async Task ValidateAsync_DeviceIdentifierMismatch_ReturnsFalse(
        User user, Guid deviceId)
    {
        var sutProvider = GetSutProvider();
        ArrangeValid(sutProvider, user, deviceId);

        Assert.False(await sutProvider.Sut.ValidateAsync(user, null, "a-different-device", _token));
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

        Assert.True(await sutProvider.Sut.ValidateAsync(user, null, _deviceIdentifier.ToUpperInvariant(), _token));
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

        Assert.False(await sutProvider.Sut.ValidateAsync(user, null, _deviceIdentifier, _token));
    }

    /// <summary>The row outlived its expiry but the sweep has not reached it.</summary>
    [Theory, BitAutoData]
    public async Task ValidateAsync_RowExpired_ReturnsFalse(
        User user, Guid deviceId)
    {
        var sutProvider = GetSutProvider();
        var (_, row) = ArrangeValid(sutProvider, user, deviceId);
        row.ExpirationDate = _now.AddMinutes(-1);

        Assert.False(await sutProvider.Sut.ValidateAsync(user, null, _deviceIdentifier, _token));
    }

    /// <summary>
    /// Expiry is exclusive of the expiry instant, matching <c>ExpiringTokenable.IsExpired</c>: a row is
    /// still honored at exactly its <c>ExpirationDate</c> and refused one tick later.
    /// </summary>
    [Theory, BitAutoData]
    public async Task ValidateAsync_RowExpiresExactlyNow_ReturnsTrue(User user, Guid deviceId)
    {
        var sutProvider = GetSutProvider();
        var (_, row) = ArrangeValid(sutProvider, user, deviceId);
        row.ExpirationDate = _now;

        Assert.True(await sutProvider.Sut.ValidateAsync(user, null, _deviceIdentifier, _token));
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_RowExpiredOneTickAgo_ReturnsFalse(User user, Guid deviceId)
    {
        var sutProvider = GetSutProvider();
        var (_, row) = ArrangeValid(sutProvider, user, deviceId);
        row.ExpirationDate = _now.AddTicks(-1);

        Assert.False(await sutProvider.Sut.ValidateAsync(user, null, _deviceIdentifier, _token));
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_TokenCannotBeUnprotected_ReturnsFalse(
        User user)
    {
        var sutProvider = GetSutProvider();
        sutProvider.GetDependency<IDataProtectorTokenFactory<TwoFactorRememberTokenable>>()
            .TryUnprotect(_token, out Arg.Any<TwoFactorRememberTokenable>())
            .Returns(c => { c[1] = null!; return false; });

        Assert.False(await sutProvider.Sut.ValidateAsync(user, null, _deviceIdentifier, _token));
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_TokenItselfExpired_ReturnsFalse(
        User user, Guid deviceId)
    {
        var sutProvider = GetSutProvider();
        var (tokenable, _) = ArrangeValid(sutProvider, user, deviceId);
        // Real clock on purpose: ExpiringTokenable.IsExpired reads DateTime.UtcNow directly, so the
        // injected TimeProvider cannot reach it.
        tokenable.ExpirationDate = DateTime.UtcNow.AddMinutes(-1);

        Assert.False(await sutProvider.Sut.ValidateAsync(user, null, _deviceIdentifier, _token));
    }

    /// <summary>Validation reads; it must never write.</summary>
    [Theory, BitAutoData]
    public async Task ValidateAsync_Always_DoesNotWrite(
        User user, Guid deviceId)
    {
        var sutProvider = GetSutProvider();
        ArrangeValid(sutProvider, user, deviceId);

        await sutProvider.Sut.ValidateAsync(user, null, _deviceIdentifier, _token);

        var repository = sutProvider.GetDependency<ITwoFactorRememberTokenRepository>();
        await repository.DidNotReceiveWithAnyArgs().UpsertAsync(default!);
        await repository.DidNotReceiveWithAnyArgs().RotateStampsByUserIdAsync(default, default);
        await repository.DidNotReceiveWithAnyArgs().DeleteExpiredAsync(default);
    }
}
