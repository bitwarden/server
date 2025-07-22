using Bit.Core.Entities;
using Bit.Core.KeyManagement.Sends;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.AspNetCore.Identity;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.KeyManagement.Sends;

[SutProviderCustomize]
public class SendPasswordHasherTests
{
    [Theory]
    [BitAutoData(PasswordVerificationResult.Success)]
    [BitAutoData(PasswordVerificationResult.SuccessRehashNeeded)]
    public void VerifyPasswordHash_WithValidMatching_ReturnsTrue(
        PasswordVerificationResult passwordVerificationResult,
        SutProvider<SendPasswordHasher> sutProvider,
        string sendPasswordHash,
        string inputPasswordHash)
    {
        // Arrange
        sutProvider.GetDependency<IPasswordHasher<User>>()
            .VerifyHashedPassword(Arg.Any<User>(), sendPasswordHash, inputPasswordHash)
            .Returns(passwordVerificationResult);

        // Act
        var result = sutProvider.Sut.VerifyPasswordHash(sendPasswordHash, inputPasswordHash);

        // Assert
        Assert.True(result);
        sutProvider.GetDependency<IPasswordHasher<User>>()
            .Received(1)
            .VerifyHashedPassword(Arg.Any<User>(), sendPasswordHash, inputPasswordHash);
    }

    [Theory, BitAutoData]
    public void VerifyPasswordHash_WithNonMatchingPasswords_ReturnsFalse(
        SutProvider<SendPasswordHasher> sutProvider,
        string sendPasswordHash,
        string inputPasswordHash)
    {
        // Arrange
        sutProvider.GetDependency<IPasswordHasher<User>>()
            .VerifyHashedPassword(Arg.Any<User>(), sendPasswordHash, inputPasswordHash)
            .Returns(PasswordVerificationResult.Failed);

        // Act
        var result = sutProvider.Sut.VerifyPasswordHash(sendPasswordHash, inputPasswordHash);

        // Assert
        Assert.False(result);
        sutProvider.GetDependency<IPasswordHasher<User>>()
            .Received(1)
            .VerifyHashedPassword(Arg.Any<User>(), sendPasswordHash, inputPasswordHash);
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
        var passwordHasher = Substitute.For<IPasswordHasher<User>>();
        var sut = new SendPasswordHasher(passwordHasher);

        // Act
        var result = sut.VerifyPasswordHash(sendPasswordHash, inputPasswordHash);

        // Assert
        Assert.False(result);
        passwordHasher.DidNotReceive().VerifyHashedPassword(Arg.Any<User>(), Arg.Any<string>(), Arg.Any<string>());
    }

    [Theory, BitAutoData]
    public void HashPasswordHash_WithValidInput_ReturnsHashedPassword(
        SutProvider<SendPasswordHasher> sutProvider,
        string clientHashedPassword,
        string expectedHashedResult)
    {
        // Arrange
        sutProvider.GetDependency<IPasswordHasher<User>>()
            .HashPassword(Arg.Any<User>(), clientHashedPassword)
            .Returns(expectedHashedResult);

        // Act
        var result = sutProvider.Sut.HashPasswordHash(clientHashedPassword);

        // Assert
        Assert.Equal(expectedHashedResult, result);
        sutProvider.GetDependency<IPasswordHasher<User>>()
            .Received(1)
            .HashPassword(Arg.Any<User>(), clientHashedPassword);
    }

    [Theory, BitAutoData]
    public void HashPasswordHash_CreatesNewUserInstance_ForPasswordHashing(
        SutProvider<SendPasswordHasher> sutProvider,
        string clientHashedPassword)
    {
        // Arrange
        User capturedUser = null;
        sutProvider.GetDependency<IPasswordHasher<User>>()
            .HashPassword(Arg.Do<User>(u => capturedUser = u), clientHashedPassword)
            .Returns("hashed_result");

        // Act
        sutProvider.Sut.HashPasswordHash(clientHashedPassword);

        // Assert
        Assert.NotNull(capturedUser);
        Assert.IsType<User>(capturedUser);
    }

    [Theory, BitAutoData]
    public void VerifyPasswordHash_CreatesNewUserInstance_ForPasswordVerification(
        SutProvider<SendPasswordHasher> sutProvider,
        string sendPasswordHash,
        string inputPasswordHash)
    {
        // Arrange
        User capturedUser = null;
        sutProvider.GetDependency<IPasswordHasher<User>>()
            .VerifyHashedPassword(Arg.Do<User>(u => capturedUser = u), sendPasswordHash, inputPasswordHash)
            .Returns(PasswordVerificationResult.Success);

        // Act
        sutProvider.Sut.VerifyPasswordHash(sendPasswordHash, inputPasswordHash);

        // Assert
        Assert.NotNull(capturedUser);
        Assert.IsType<User>(capturedUser);
    }

    [Theory, BitAutoData]
    public void VerifyPasswordHash_WithMultipleCalls_CreatesNewUserInstanceEachTime(
        SutProvider<SendPasswordHasher> sutProvider,
        string sendPasswordHash1,
        string inputPasswordHash1,
        string sendPasswordHash2,
        string inputPasswordHash2)
    {
        // Arrange
        var capturedUsers = new List<User>();
        sutProvider.GetDependency<IPasswordHasher<User>>()
            .VerifyHashedPassword(Arg.Do<User>(u => capturedUsers.Add(u)), Arg.Any<string>(), Arg.Any<string>())
            .Returns(PasswordVerificationResult.Success);

        // Act
        sutProvider.Sut.VerifyPasswordHash(sendPasswordHash1, inputPasswordHash1);
        sutProvider.Sut.VerifyPasswordHash(sendPasswordHash2, inputPasswordHash2);

        // Assert
        Assert.Equal(2, capturedUsers.Count);
        Assert.NotSame(capturedUsers[0], capturedUsers[1]);
    }

    [Theory, BitAutoData]
    // Even when the input is the same, a new User instance should be created each time
    public void HashPasswordHash_WithMultipleCalls_CreatesNewUserInstanceEachTime(
        SutProvider<SendPasswordHasher> sutProvider,
        string clientHashedPassword1,
        string clientHashedPassword2)
    {
        // Arrange
        var capturedUsers = new List<User>();
        sutProvider.GetDependency<IPasswordHasher<User>>()
            .HashPassword(Arg.Do<User>(u => capturedUsers.Add(u)), Arg.Any<string>())
            .Returns("hashed_result");

        // Act
        sutProvider.Sut.HashPasswordHash(clientHashedPassword1);
        sutProvider.Sut.HashPasswordHash(clientHashedPassword2);

        // Assert
        Assert.Equal(2, capturedUsers.Count);
        Assert.NotSame(capturedUsers[0], capturedUsers[1]);
    }
}
