using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Identity.IdentityServer.RequestValidators;
using Bit.Test.Common.Constants;
using NSubstitute;
using Xunit;

namespace Bit.Identity.Test.IdentityServer.RequestValidators;

public class ClientVersionValidatorTests
{
    private static ICurrentContext MakeContext(Version? version, DeviceType? deviceType = null)
    {
        var ctx = Substitute.For<ICurrentContext>();
        ctx.ClientVersion = version;
        ctx.DeviceType = deviceType;
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

    private static User MakeEncryptionV1User()
    {
        return new User
        {
            PrivateKey = TestEncryptionConstants.AES256_CBC_HMAC_Encstring,
            SecurityVersion = null
        };
    }

    [Theory]
    [InlineData(null, "2026.10.1")]
    [InlineData(DeviceType.Android, "2026.10.1")]
    [InlineData(DeviceType.iOS, "2026.10.1")]
    [InlineData(DeviceType.ChromeExtension, "2026.10.1")]
    [InlineData(DeviceType.ChromeBrowser, "2026.10.1")]
    [InlineData(DeviceType.LinuxCLI, "2026.10.1")]
    [InlineData(DeviceType.Android, "2026.11.0")]
    [InlineData(DeviceType.ChromeExtension, "2027.1.0")]
    public void Allows_When_EncryptionV2UserOnNonDesktopClientMeetsMinimumVersion(DeviceType? deviceType, string clientVersion)
    {
        // Arrange
        var sut = new ClientVersionValidator(MakeContext(new Version(clientVersion), deviceType));
        var ctx = new Bit.Identity.IdentityServer.CustomValidatorRequestContext();
        var user = MakeValidV2User();

        // Act
        var ok = sut.Validate(user, ctx);

        // Assert
        Assert.True(ok);
    }

    [Theory]
    [InlineData(null, "2026.10.0")]
    [InlineData(DeviceType.Android, "2026.10.0")]
    [InlineData(DeviceType.iOS, "2026.9.1")]
    [InlineData(DeviceType.ChromeExtension, "2026.10.0")]
    [InlineData(DeviceType.ChromeBrowser, "2026.9.1")]
    [InlineData(DeviceType.LinuxCLI, "2025.12.0")]
    public void Blocks_When_EncryptionV2UserOnNonDesktopClientTooOld(DeviceType? deviceType, string clientVersion)
    {
        // Arrange
        var sut = new ClientVersionValidator(MakeContext(new Version(clientVersion), deviceType));
        var ctx = new Bit.Identity.IdentityServer.CustomValidatorRequestContext();
        var user = MakeValidV2User();

        // Act
        var ok = sut.Validate(user, ctx);

        // Assert
        Assert.False(ok);
        Assert.NotNull(ctx.ValidationErrorResult);
        Assert.True(ctx.ValidationErrorResult.IsError);
        Assert.Equal("invalid_client_version", ctx.ValidationErrorResult.Error);
    }

    [Theory]
    [InlineData(DeviceType.WindowsDesktop, "2026.10.2")]
    [InlineData(DeviceType.MacOsDesktop, "2026.10.2")]
    [InlineData(DeviceType.LinuxDesktop, "2026.10.2")]
    [InlineData(DeviceType.UWP, "2026.10.2")]
    [InlineData(DeviceType.WindowsDesktop, "2026.10.4")]
    [InlineData(DeviceType.MacOsDesktop, "2027.1.0")]
    public void Allows_When_EncryptionV2UserOnDesktopClientMeetsMinimumVersion(DeviceType deviceType, string clientVersion)
    {
        // Arrange
        var sut = new ClientVersionValidator(MakeContext(new Version(clientVersion), deviceType));
        var ctx = new Bit.Identity.IdentityServer.CustomValidatorRequestContext();
        var user = MakeValidV2User();

        // Act
        var ok = sut.Validate(user, ctx);

        // Assert
        Assert.True(ok);
    }

    [Theory]
    [InlineData(DeviceType.WindowsDesktop, "2026.10.1")]
    [InlineData(DeviceType.MacOsDesktop, "2026.10.1")]
    [InlineData(DeviceType.LinuxDesktop, "2026.10.1")]
    [InlineData(DeviceType.UWP, "2026.10.1")]
    [InlineData(DeviceType.WindowsDesktop, "2026.9.1")]
    public void Blocks_When_EncryptionV2UserOnDesktopClientTooOld(DeviceType deviceType, string clientVersion)
    {
        // Arrange
        var sut = new ClientVersionValidator(MakeContext(new Version(clientVersion), deviceType));
        var ctx = new Bit.Identity.IdentityServer.CustomValidatorRequestContext();
        var user = MakeValidV2User();

        // Act
        var ok = sut.Validate(user, ctx);

        // Assert
        Assert.False(ok);
        Assert.NotNull(ctx.ValidationErrorResult);
        Assert.True(ctx.ValidationErrorResult.IsError);
        Assert.Equal("invalid_client_version", ctx.ValidationErrorResult.Error);
    }

    [Theory]
    [InlineData(null, "2026.10.0")]
    [InlineData(DeviceType.Android, "2026.10.0")]
    [InlineData(DeviceType.iOS, "2026.9.1")]
    [InlineData(DeviceType.ChromeExtension, "2026.10.0")]
    [InlineData(DeviceType.ChromeBrowser, "2026.9.1")]
    [InlineData(DeviceType.LinuxCLI, "2025.12.0")]
    [InlineData(DeviceType.WindowsDesktop, "2026.10.1")]
    [InlineData(DeviceType.MacOsDesktop, "2026.10.1")]
    [InlineData(DeviceType.LinuxDesktop, "2026.10.1")]
    [InlineData(DeviceType.UWP, "2026.9.1")]
    public void Allows_When_EncryptionV1UserOnClientBelowEncryptionV2MinimumVersion(DeviceType? deviceType, string clientVersion)
    {
        // Arrange
        var sut = new ClientVersionValidator(MakeContext(new Version(clientVersion), deviceType));
        var ctx = new Bit.Identity.IdentityServer.CustomValidatorRequestContext();
        var user = MakeEncryptionV1User();

        // Act
        var ok = sut.Validate(user, ctx);

        // Assert
        Assert.True(ok);
        Assert.Null(ctx.ValidationErrorResult);
    }

    [Fact]
    public void Blocks_When_NullUser()
    {
        // Arrange
        var sut = new ClientVersionValidator(MakeContext(new Version("2026.10.1")));
        var ctx = new Bit.Identity.IdentityServer.CustomValidatorRequestContext();
        User? user = null;

        // Act
        var ok = sut.Validate(user, ctx);

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
        var sut = new ClientVersionValidator(MakeContext(new Version("2026.10.0")));
        var ctx = new Bit.Identity.IdentityServer.CustomValidatorRequestContext();
        var user = MakeValidV2User();
        user.PrivateKey = null;

        // Act
        var ok = sut.Validate(user, ctx);

        // Assert
        Assert.True(ok);
    }

    [Fact]
    public void Allows_When_NoSecurityVersion()
    {
        // Arrange
        var sut = new ClientVersionValidator(MakeContext(new Version("2026.10.0")));
        var ctx = new Bit.Identity.IdentityServer.CustomValidatorRequestContext();

        var user = MakeValidV2User();
        user.SecurityVersion = null;
        // Act
        var ok = sut.Validate(user, ctx);
        // Assert
        Assert.True(ok);
    }

    [Fact]
    public void Blocks_When_ClientVersionHeaderMissing()
    {
        // Arrange
        var sut = new ClientVersionValidator(MakeContext(null));
        var ctx = new Bit.Identity.IdentityServer.CustomValidatorRequestContext();
        var user = MakeValidV2User();

        // Act
        var ok = sut.Validate(user, ctx);

        // Assert
        Assert.False(ok);
        Assert.NotNull(ctx.ValidationErrorResult);
        Assert.True(ctx.ValidationErrorResult.IsError);
        Assert.Equal("version_header_missing", ctx.ValidationErrorResult.Error);
    }
}
