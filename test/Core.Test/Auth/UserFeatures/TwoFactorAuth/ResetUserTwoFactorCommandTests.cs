using Bit.Core.Auth.UserFeatures.TwoFactorAuth.Implementations;
using Bit.Core.Entities;
using Bit.Core.Repositories;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.Auth.UserFeatures.TwoFactorAuth;

[SutProviderCustomize]
public class ResetUserTwoFactorCommandTests
{
    private static SutProvider<ResetUserTwoFactorCommand> GetSutProvider()
    {
        return new SutProvider<ResetUserTwoFactorCommand>()
            .WithFakeTimeProvider()
            .Create();
    }

    [Theory, BitAutoData]
    public async Task ResetAsync_SetsTwoFactorProvidersToNull(User user)
    {
        // Arrange
        user.TwoFactorProviders = "something";
        var sutProvider = GetSutProvider();

        // Act
        await sutProvider.Sut.ResetAsync(user);

        // Assert
        Assert.Null(user.TwoFactorProviders);
    }

    [Theory, BitAutoData]
    public async Task ResetAsync_SetsTwoFactorRecoveryCodeToNull(User user)
    {
        // Arrange
        user.TwoFactorRecoveryCode = "recoveryCode";
        var sutProvider = GetSutProvider();

        // Act
        await sutProvider.Sut.ResetAsync(user);

        // Assert
        Assert.Null(user.TwoFactorRecoveryCode);
    }

    [Theory, BitAutoData]
    public async Task ResetAsync_BumpsRevisionAndAccountRevisionDates(User user)
    {
        // Arrange
        var sutProvider = GetSutProvider();
        var now = new DateTime(2024, 6, 15, 12, 0, 0, DateTimeKind.Utc);
        sutProvider.GetDependency<FakeTimeProvider>().SetUtcNow(now);

        // Act
        await sutProvider.Sut.ResetAsync(user);

        // Assert
        Assert.Equal(now, user.RevisionDate);
        Assert.Equal(now, user.AccountRevisionDate);
    }

    [Theory, BitAutoData]
    public async Task ResetAsync_CallsUserRepositoryReplace(User user)
    {
        // Arrange
        var sutProvider = GetSutProvider();

        // Act
        await sutProvider.Sut.ResetAsync(user);

        // Assert
        await sutProvider.GetDependency<IUserRepository>()
            .Received(1)
            .ReplaceAsync(user);
    }
}
