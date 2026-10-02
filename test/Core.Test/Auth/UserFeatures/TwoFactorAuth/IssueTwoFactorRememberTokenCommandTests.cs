using Bit.Core.Auth.Entities;
using Bit.Core.Auth.Models.Business.Tokenables;
using Bit.Core.Auth.Repositories;
using Bit.Core.Auth.UserFeatures.TwoFactorAuth.Implementations;
using Bit.Core.Entities;
using Bit.Core.Tokens;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.Auth.UserFeatures.TwoFactorAuth;

[SutProviderCustomize]
public class IssueTwoFactorRememberTokenCommandTests
{
    private static readonly DateTime _expiration = new(2026, 11, 1, 12, 0, 0, DateTimeKind.Utc);

    /// <summary>
    /// Stands in for the real factory: binds the user and device it is given, carries whatever stamp the
    /// command generated, and expires at a fixed instant.
    /// </summary>
    private static void ArrangeFactory(
        SutProvider<IssueTwoFactorRememberTokenCommand> sutProvider, User user, Device device)
    {
        sutProvider.GetDependency<ITwoFactorRememberTokenableFactory>()
            .CreateToken(user, device, Arg.Any<string>())
            .Returns(c => new TwoFactorRememberTokenable(
                user.Id, device.Id, device.Identifier, c.ArgAt<string>(2), user.SecurityStamp)
            {
                ExpirationDate = _expiration,
            });
    }

    /// <summary>
    /// The stamp written to the row is the stamp embedded in the token, or validation could
    /// never succeed.
    /// </summary>
    [Theory, BitAutoData]
    public async Task IssueAsync_UpsertsRowWithStampMatchingToken(
        SutProvider<IssueTwoFactorRememberTokenCommand> sutProvider,
        User user,
        Device device)
    {
        device.UserId = user.Id;
        ArrangeFactory(sutProvider, user, device);
        TwoFactorRememberToken? saved = null;
        sutProvider.GetDependency<ITwoFactorRememberTokenRepository>()
            .UpsertAsync(Arg.Do<TwoFactorRememberToken>(t => saved = t))
            .Returns(c => c.Arg<TwoFactorRememberToken>());

        TwoFactorRememberTokenable? minted = null;
        sutProvider.GetDependency<IDataProtectorTokenFactory<TwoFactorRememberTokenable>>()
            .Protect(Arg.Do<TwoFactorRememberTokenable>(t => minted = t))
            .Returns("protected-token");

        var result = await sutProvider.Sut.IssueAsync(user, device);

        Assert.Equal("protected-token", result);
        Assert.NotNull(saved);
        Assert.NotNull(minted);
        Assert.Equal(user.Id, saved.UserId);
        Assert.Equal(device.Id, saved.DeviceId);
        Assert.Equal(saved.Stamp, minted.Stamp);
        Assert.Equal(user.Id, minted.UserId);
        Assert.Equal(device.Id, minted.DeviceId);
        Assert.Equal(device.Identifier, minted.DeviceIdentifier);
        Assert.Equal(user.SecurityStamp, minted.SecurityStamp);
    }

    /// <summary>
    /// Re-remembering a device writes a different stamp, which is what orphans the token that
    /// device was holding.
    /// </summary>
    [Theory, BitAutoData]
    public async Task IssueAsync_CalledTwice_WritesDifferentStampEachTime(
        SutProvider<IssueTwoFactorRememberTokenCommand> sutProvider,
        User user,
        Device device)
    {
        device.UserId = user.Id;
        ArrangeFactory(sutProvider, user, device);
        var stamps = new List<string>();
        sutProvider.GetDependency<ITwoFactorRememberTokenRepository>()
            .UpsertAsync(Arg.Do<TwoFactorRememberToken>(t => stamps.Add(t.Stamp)))
            .Returns(c => c.Arg<TwoFactorRememberToken>());

        await sutProvider.Sut.IssueAsync(user, device);
        await sutProvider.Sut.IssueAsync(user, device);

        Assert.Equal(2, stamps.Count);
        Assert.NotEqual(stamps[0], stamps[1]);
    }

    /// <summary>
    /// The row takes its expiry from the minted token, so the two lapse at the same instant.
    /// </summary>
    [Theory, BitAutoData]
    public async Task IssueAsync_RowAndTokenShareExpiration(
        SutProvider<IssueTwoFactorRememberTokenCommand> sutProvider,
        User user,
        Device device)
    {
        device.UserId = user.Id;
        ArrangeFactory(sutProvider, user, device);
        TwoFactorRememberToken? saved = null;
        sutProvider.GetDependency<ITwoFactorRememberTokenRepository>()
            .UpsertAsync(Arg.Do<TwoFactorRememberToken>(t => saved = t))
            .Returns(c => c.Arg<TwoFactorRememberToken>());

        TwoFactorRememberTokenable? minted = null;
        sutProvider.GetDependency<IDataProtectorTokenFactory<TwoFactorRememberTokenable>>()
            .Protect(Arg.Do<TwoFactorRememberTokenable>(t => minted = t))
            .Returns("protected-token");

        await sutProvider.Sut.IssueAsync(user, device);

        Assert.NotNull(saved);
        Assert.NotNull(minted);
        Assert.Equal(_expiration, saved.ExpirationDate);
        Assert.Equal(_expiration, minted.ExpirationDate);
    }

    /// <summary>
    /// Stamps are lower-case GUID text on every write path, which is what makes the ordinal,
    /// case-sensitive comparison at validation time safe.
    /// </summary>
    [Theory, BitAutoData]
    public async Task IssueAsync_StampIsLowerCaseGuidText(
        SutProvider<IssueTwoFactorRememberTokenCommand> sutProvider,
        User user,
        Device device)
    {
        device.UserId = user.Id;
        ArrangeFactory(sutProvider, user, device);
        TwoFactorRememberToken? saved = null;
        sutProvider.GetDependency<ITwoFactorRememberTokenRepository>()
            .UpsertAsync(Arg.Do<TwoFactorRememberToken>(t => saved = t))
            .Returns(c => c.Arg<TwoFactorRememberToken>());

        await sutProvider.Sut.IssueAsync(user, device);

        Assert.NotNull(saved);
        Assert.True(Guid.TryParse(saved.Stamp, out _));
        Assert.Equal(saved.Stamp.ToLowerInvariant(), saved.Stamp);
    }
}
