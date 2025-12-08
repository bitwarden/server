using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Identity.IdentityServer.RequestValidators;
using Bit.Test.Common.Constants;
using NSubstitute;
using Xunit;

namespace Bit.Identity.Test.IdentityServer.RequestValidators;

public class ClientVersionValidatorTests
{
    private static ICurrentContext MakeContext(Version? version)
    {
        var ctx = Substitute.For<ICurrentContext>();
        ctx.ClientVersion = version;
        return ctx;
    }

    private static User MakeValidV2User()
    {
        return new User
        {
            PrivateKey = TestEncryptionConstants.V2PrivateKey,
            SecurityVersion = 2
        };
    }

    [Fact]
    public void Allows_When_ClientMeetsMinimumVersion()
    {
        // Arrange
        var sut = new ClientVersionValidator(MakeContext(new Version("2025.11.0")));
        var ctx = new Bit.Identity.IdentityServer.CustomValidatorRequestContext();
        var user = MakeValidV2User();

        // Act
        var ok = sut.ValidateAsync(user, ctx);

        // Assert
        Assert.True(ok);
    }

    [Fact]
    public void Blocks_When_ClientTooOld()
    {
        // Arrange
        var sut = new ClientVersionValidator(MakeContext(new Version("2025.10.0")));
        var ctx = new Bit.Identity.IdentityServer.CustomValidatorRequestContext();
        var user = MakeValidV2User();

        // Act
        var ok = sut.ValidateAsync(user, ctx);

        // Assert
        Assert.False(ok);
        Assert.NotNull(ctx.ValidationErrorResult);
        Assert.True(ctx.ValidationErrorResult.IsError);
        Assert.Equal("invalid_client_version", ctx.ValidationErrorResult.Error);
    }

    [Fact]
    public void Blocks_When_NullUser()
    {
        // Arrange
        var sut = new ClientVersionValidator(MakeContext(new Version("2025.11.0")));
        var ctx = new Bit.Identity.IdentityServer.CustomValidatorRequestContext();
        User? user = null;

        // Act
        var ok = sut.ValidateAsync(user, ctx);

        // Assert
        Assert.False(ok);
        Assert.NotNull(ctx.ValidationErrorResult);
        Assert.True(ctx.ValidationErrorResult.IsError);
        Assert.Equal("no_user", ctx.ValidationErrorResult.Error);
    }

    [Fact]
    public void Allows_When_NoPrivateKey()
    {
        // Arrange
        var sut = new ClientVersionValidator(MakeContext(new Version("2025.11.0")));
        var ctx = new Bit.Identity.IdentityServer.CustomValidatorRequestContext();
        var user = MakeValidV2User();
        user.PrivateKey = null;

        // Act
        var ok = sut.ValidateAsync(user, ctx);

        // Assert
        Assert.True(ok);
    }

    [Fact]
    public void Allows_When_NoSecurityVersion()
    {
        // Arrange
        var sut = new ClientVersionValidator(MakeContext(new Version("2025.11.0")));
        var ctx = new Bit.Identity.IdentityServer.CustomValidatorRequestContext();

        var user = MakeValidV2User();
        user.SecurityVersion = null;
        // Act
        var ok = sut.ValidateAsync(user, ctx);
        // Assert
        Assert.True(ok);
    }

    [Fact]
    public void Allows_When_ClientVersionHeaderMissing()
    {
        // Arrange
        var sut = new ClientVersionValidator(MakeContext(null));
        var ctx = new Bit.Identity.IdentityServer.CustomValidatorRequestContext();
        var user = MakeValidV2User();

        // Act
        var ok = sut.ValidateAsync(user, ctx);

        // Assert
        Assert.True(ok);
    }
}
