using Bit.Core.Auth.Repositories;
using Bit.Core.Auth.UserFeatures.TwoFactorAuth.Implementations;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.Auth.UserFeatures.TwoFactorAuth;

[SutProviderCustomize]
public class RevokeTwoFactorRememberTokensCommandTests
{
    [Theory, BitAutoData]
    public async Task RevokeAllForUserAsync_RotatesStampsForThatUser(
        SutProvider<RevokeTwoFactorRememberTokensCommand> sutProvider,
        Guid userId)
    {
        await sutProvider.Sut.RevokeAllForUserAsync(userId);

        await sutProvider.GetDependency<ITwoFactorRememberTokenRepository>()
            .Received(1)
            .RotateStampsByUserIdAsync(userId);
    }

    /// <summary>
    /// Revocation is scoped to remember-me. It must not delete rows, which would destroy the record
    /// of when each device was first remembered.
    /// </summary>
    [Theory, BitAutoData]
    public async Task RevokeAllForUserAsync_DoesNotDeleteRows(
        SutProvider<RevokeTwoFactorRememberTokensCommand> sutProvider,
        Guid userId)
    {
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
    public async Task RevokeAllForUserAsync_RepositoryThrows_Propagates(
        SutProvider<RevokeTwoFactorRememberTokensCommand> sutProvider,
        Guid userId)
    {
        sutProvider.GetDependency<ITwoFactorRememberTokenRepository>()
            .RotateStampsByUserIdAsync(userId)
            .Returns(Task.FromException(new InvalidOperationException("database unavailable")));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => sutProvider.Sut.RevokeAllForUserAsync(userId));
    }
}
