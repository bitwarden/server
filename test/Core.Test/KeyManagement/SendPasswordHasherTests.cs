using Bit.Core.KeyManagement.Sends;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.AspNetCore.Identity;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.KeyManagement.SendPasswordHasherTests;

[SutProviderCustomize]
public class SendPasswordHasherTests
{
    [Theory]
    [BitAutoData(PasswordVerificationResult.Success)]
    [BitAutoData(PasswordVerificationResult.SuccessRehashNeeded)]
    void VerifyPasswordHash_WithValidMatching_ReturnsTrue(
        PasswordVerificationResult passwordVerificationResult,
        SutProvider<SendPasswordHasher> sutProvider,
        string sendPasswordHash,
        string inputPasswordHash)
    {
        // Arrange
        sutProvider.GetDependency<IPasswordHasher<SendPasswordHasherMarker>>()
            .VerifyHashedPassword(Arg.Any<SendPasswordHasherMarker>(), sendPasswordHash, inputPasswordHash)
            .Returns(passwordVerificationResult);

        // Act
        var result = sutProvider.Sut.PasswordHashMatches(sendPasswordHash, inputPasswordHash);

        // Assert
        Assert.True(result);
        sutProvider.GetDependency<IPasswordHasher<SendPasswordHasherMarker>>()
            .Received(1)
            .VerifyHashedPassword(Arg.Any<SendPasswordHasherMarker>(), sendPasswordHash, inputPasswordHash);
    }

    [Theory, BitAutoData]
    void VerifyPasswordHash_WithNonMatchingPasswords_ReturnsFalse(
        SutProvider<SendPasswordHasher> sutProvider,
        string sendPasswordHash,
        string inputPasswordHash)
    {
        // Arrange
        sutProvider.GetDependency<IPasswordHasher<SendPasswordHasherMarker>>()
            .VerifyHashedPassword(Arg.Any<SendPasswordHasherMarker>(), sendPasswordHash, inputPasswordHash)
            .Returns(PasswordVerificationResult.Failed);

        // Act
        var result = sutProvider.Sut.PasswordHashMatches(sendPasswordHash, inputPasswordHash);

        // Assert
        Assert.False(result);
        sutProvider.GetDependency<IPasswordHasher<SendPasswordHasherMarker>>()
            .Received(1)
            .VerifyHashedPassword(Arg.Any<SendPasswordHasherMarker>(), sendPasswordHash, inputPasswordHash);
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
        var passwordHasher = Substitute.For<IPasswordHasher<SendPasswordHasherMarker>>();
        var sut = new SendPasswordHasher(passwordHasher);

        // Act
        var result = sut.PasswordHashMatches(sendPasswordHash, inputPasswordHash);

        // Assert
        Assert.False(result);
        passwordHasher.DidNotReceive().VerifyHashedPassword(Arg.Any<SendPasswordHasherMarker>(), Arg.Any<string>(), Arg.Any<string>());
    }

    [Theory, BitAutoData]
    void HashPasswordHash_WithValidInput_ReturnsHashedPassword(
        SutProvider<SendPasswordHasher> sutProvider,
        string clientHashedPassword,
        string expectedHashedResult)
    {
        // Arrange
        sutProvider.GetDependency<IPasswordHasher<SendPasswordHasherMarker>>()
            .HashPassword(Arg.Any<SendPasswordHasherMarker>(), clientHashedPassword)
            .Returns(expectedHashedResult);

        // Act
        var result = sutProvider.Sut.HashOfClientPasswordHash(clientHashedPassword);

        // Assert
        Assert.Equal(expectedHashedResult, result);
        sutProvider.GetDependency<IPasswordHasher<SendPasswordHasherMarker>>()
            .Received(1)
            .HashPassword(Arg.Any<SendPasswordHasherMarker>(), clientHashedPassword);
    }
}
