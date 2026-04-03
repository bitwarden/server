using Bit.Core.Auth.UserFeatures.UserMasterPassword;
using Bit.Core.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.Auth.UserFeatures.UserMasterPassword;

public class MasterPasswordHasherServiceTests
{
    private readonly IPasswordHasher<User> _passwordHasher = Substitute.For<IPasswordHasher<User>>();
    private readonly UserManager<User> _userManager;
    private readonly ILogger<MasterPasswordHasherService> _logger = Substitute.For<ILogger<MasterPasswordHasherService>>();

    public MasterPasswordHasherServiceTests()
    {
        var userStore = Substitute.For<IUserStore<User>>();
        _userManager = Substitute.For<UserManager<User>>(
            userStore, null, null, null, null, null, null, null, null);
    }

    [Fact]
    public async Task ValidateAndHashPasswordAsync_NoValidators_ReturnsSuccessAndHash()
    {
        var user = new User { Email = "test@example.com" };
        var clientSideHash = "client-hash";
        var expectedServerHash = "server-hash";

        _passwordHasher.HashPassword(user, clientSideHash).Returns(expectedServerHash);

        var sut = new MasterPasswordHasherService(
            _passwordHasher,
            Enumerable.Empty<IPasswordValidator<User>>(),
            _userManager,
            _logger);

        var (result, serverSideHash) = await sut.ValidateAndHashPasswordAsync(user, clientSideHash);

        Assert.True(result.Succeeded);
        Assert.Equal(expectedServerHash, serverSideHash);
    }

    [Fact]
    public async Task ValidateAndHashPasswordAsync_ValidationPasses_ReturnsSuccessAndHash()
    {
        var user = new User { Email = "test@example.com" };
        var clientSideHash = "client-hash";
        var expectedServerHash = "server-hash";

        _passwordHasher.HashPassword(user, clientSideHash).Returns(expectedServerHash);

        var validator = Substitute.For<IPasswordValidator<User>>();
        validator.ValidateAsync(_userManager, user, clientSideHash)
            .Returns(IdentityResult.Success);

        var sut = new MasterPasswordHasherService(
            _passwordHasher,
            new[] { validator },
            _userManager,
            _logger);

        var (result, serverSideHash) = await sut.ValidateAndHashPasswordAsync(user, clientSideHash);

        Assert.True(result.Succeeded);
        Assert.Equal(expectedServerHash, serverSideHash);
    }

    [Fact]
    public async Task ValidateAndHashPasswordAsync_ValidationFails_ReturnsFailure()
    {
        var user = new User { Email = "test@example.com" };
        var clientSideHash = "weak-password";
        var validationError = new IdentityError { Code = "PasswordTooShort", Description = "Too short" };

        var validator = Substitute.For<IPasswordValidator<User>>();
        validator.ValidateAsync(_userManager, user, clientSideHash)
            .Returns(IdentityResult.Failed(validationError));

        var sut = new MasterPasswordHasherService(
            _passwordHasher,
            new[] { validator },
            _userManager,
            _logger);

        var (result, serverSideHash) = await sut.ValidateAndHashPasswordAsync(user, clientSideHash);

        Assert.False(result.Succeeded);
        Assert.Null(serverSideHash);
        Assert.Contains(result.Errors, e => e.Code == "PasswordTooShort");
    }

    [Fact]
    public void HashPassword_DelegatesToPasswordHasher()
    {
        var user = new User { Email = "test@example.com" };
        var clientSideHash = "client-hash";
        var expectedServerHash = "server-hash";

        _passwordHasher.HashPassword(user, clientSideHash).Returns(expectedServerHash);

        var sut = new MasterPasswordHasherService(
            _passwordHasher,
            Enumerable.Empty<IPasswordValidator<User>>(),
            _userManager,
            _logger);

        var result = sut.HashPassword(user, clientSideHash);

        Assert.Equal(expectedServerHash, result);
    }
}
