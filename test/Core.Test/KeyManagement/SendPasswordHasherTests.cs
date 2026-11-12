using Bit.Core.KeyManagement.Sends;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Bitwarden.Server.Sdk.PasswordHashing;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.KeyManagement.Sends;

[SutProviderCustomize]
public class SendPasswordHasherTests
{
    [Theory]
    [BitAutoData(PasswordHashVerification.Success)]
    [BitAutoData(PasswordHashVerification.SuccessRehashNeeded)]
    void VerifyPasswordHash_WithValidMatching_ReturnsTrue(
        PasswordHashVerification verification,
        SutProvider<SendPasswordHasher> sutProvider,
        string sendPasswordHash,
        string inputPasswordHash)
    {
        // Arrange
        sutProvider.GetDependency<IServerPasswordHasher>()
            .VerifyStoredHash(new ServerStorableHash(sendPasswordHash), new ClientCreatedHash(inputPasswordHash))
            .Returns(verification);

        // Act
        var result = sutProvider.Sut.PasswordHashMatches(sendPasswordHash, inputPasswordHash);

        // Assert
        Assert.True(result);
    }

    [Theory, BitAutoData]
    void VerifyPasswordHash_WithNonMatchingPasswords_ReturnsFalse(
        SutProvider<SendPasswordHasher> sutProvider,
        string sendPasswordHash,
        string inputPasswordHash)
    {
        // Arrange
        sutProvider.GetDependency<IServerPasswordHasher>()
            .VerifyStoredHash(new ServerStorableHash(sendPasswordHash), new ClientCreatedHash(inputPasswordHash))
            .Returns(PasswordHashVerification.Failed);

        // Act
        var result = sutProvider.Sut.PasswordHashMatches(sendPasswordHash, inputPasswordHash);

        // Assert
        Assert.False(result);
    }

    [Theory]
    [InlineData(null, "inputPassword")]
    [InlineData("", "inputPassword")]
    [InlineData("   ", "inputPassword")]
    [InlineData("sendPassword", null)]
    [InlineData("sendPassword", "")]
    [InlineData("sendPassword", "   ")]
    [InlineData(null, null)]
    [InlineData("", "")]
    public void VerifyPasswordHash_WithNullOrEmptyParameters_ReturnsFalse(
        string? sendPasswordHash,
        string? inputPasswordHash)
    {
        // Arrange
        var hasher = new ServiceCollection().AddServerPasswordHasher().BuildServiceProvider()
            .GetRequiredService<IServerPasswordHasher>();
        var sut = new SendPasswordHasher(hasher);

        // Act
        var result = sut.PasswordHashMatches(sendPasswordHash!, inputPasswordHash!);

        // Assert
        Assert.False(result);
    }

    [Theory, BitAutoData]
    void HashPasswordHash_WithValidInput_ReturnsHashedPassword(
        SutProvider<SendPasswordHasher> sutProvider,
        string clientHashedPassword,
        string expectedHashedResult)
    {
        // Arrange
        sutProvider.GetDependency<IServerPasswordHasher>()
            .CreateStoredHash(new ClientCreatedHash(clientHashedPassword))
            .Returns(new ServerStorableHash(expectedHashedResult));

        // Act
        var result = sutProvider.Sut.HashOfClientPasswordHash(clientHashedPassword);

        // Assert
        Assert.Equal(expectedHashedResult, result);
    }
}
