using Bit.Core.Auth.Repositories;
using Bit.Core.Auth.UserFeatures.TwoFactorAuth.Implementations;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.Auth.UserFeatures.TwoFactorAuth;

[SutProviderCustomize]
public class RevokeTwoFactorRememberTokensCommandTests
{
    private static readonly DateTime _now = new(2026, 10, 2, 12, 0, 0, DateTimeKind.Utc);

    private static SutProvider<RevokeTwoFactorRememberTokensCommand> GetSutProvider()
    {
        var sutProvider = new SutProvider<RevokeTwoFactorRememberTokensCommand>()
            .WithFakeTimeProvider()
            .Create();
        sutProvider.GetDependency<FakeTimeProvider>().SetUtcNow(_now);
        return sutProvider;
    }

    [Theory, BitAutoData]
    public async Task RevokeAllForUserAsync_RotatesStampsForThatUser(Guid userId)
    {
        var sutProvider = GetSutProvider();

        await sutProvider.Sut.RevokeAllForUserAsync(userId);

        await sutProvider.GetDependency<ITwoFactorRememberTokenRepository>()
            .Received(1)
            .RotateStampsByUserIdAsync(userId, _now);
    }

    /// <summary>
    /// Revocation is scoped to remember-me. It must not delete rows, which would destroy the record
    /// of when each device was first remembered.
    /// </summary>
    [Theory, BitAutoData]
    public async Task RevokeAllForUserAsync_DoesNotDeleteRows(Guid userId)
    {
        var sutProvider = GetSutProvider();

        await sutProvider.Sut.RevokeAllForUserAsync(userId);

        await sutProvider.GetDependency<ITwoFactorRememberTokenRepository>()
            .DidNotReceiveWithAnyArgs()
            .DeleteExpiredAsync(default);
    }

    /// <summary>
    /// Failures propagate. Call sites rely on this: they revoke before the write that commits a
    /// teardown so that a failure leaves a state the caller can retry.
    /// </summary>
    [Theory, BitAutoData]
    public async Task RevokeAllForUserAsync_RepositoryThrows_Propagates(Guid userId)
    {
        var sutProvider = GetSutProvider();
        sutProvider.GetDependency<ITwoFactorRememberTokenRepository>()
            .RotateStampsByUserIdAsync(userId, _now)
            .Returns(Task.FromException(new InvalidOperationException("database unavailable")));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => sutProvider.Sut.RevokeAllForUserAsync(userId));
    }
}
