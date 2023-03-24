using Bit.Core.Auth.Models.Api.Request.Accounts;
using Bit.Core.Auth.Services;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Models.Data;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Identity.Controllers;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace Bit.Identity.Test.Controllers;

public class AccountsControllerTests : IDisposable
{

    private readonly AccountsController _sut;
    private readonly ILogger<AccountsController> _logger;
    private readonly IUserRepository _userRepository;
    private readonly IUserService _userService;
    private readonly ICaptchaValidationService _captchaValidationService;

    public AccountsControllerTests()
    {
        _logger = Substitute.For<ILogger<AccountsController>>();
        _userRepository = Substitute.For<IUserRepository>();
        _userService = Substitute.For<IUserService>();
        _captchaValidationService = Substitute.For<ICaptchaValidationService>();
        _sut = new AccountsController(
            _logger,
            _userRepository,
            _userService,
            _captchaValidationService
        );
    }

    public void Dispose()
    {
        _sut?.Dispose();
    }

    [Fact]
    public async Task PostPrelogin_WhenUserExists_ShouldReturnUserKdfInfo()
    {
        var userKdfInfo = new UserKdfInformation
        {
            Kdf = KdfType.PBKDF2_SHA256,
            KdfIterations = 5000
        };
        _userRepository.GetKdfInformationByEmailAsync(Arg.Any<string>()).Returns(Task.FromResult(userKdfInfo));

        var response = await _sut.PostPrelogin(new PreloginRequestModel { Email = "user@example.com" });

        Assert.Equal(userKdfInfo.Kdf, response.Kdf);
        Assert.Equal(userKdfInfo.KdfIterations, response.KdfIterations);
    }

    [Fact]
    public async Task PostPrelogin_WhenUserDoesNotExist_ShouldDefaultToSha256And100000Iterations()
    {
        _userRepository.GetKdfInformationByEmailAsync(Arg.Any<string>()).Returns(Task.FromResult<UserKdfInformation>(null!));

        var response = await _sut.PostPrelogin(new PreloginRequestModel { Email = "user@example.com" });

        Assert.Equal(KdfType.PBKDF2_SHA256, response.Kdf);
        Assert.Equal(100000, response.KdfIterations);
    }

    [Fact]
    public async Task PostRegister_ShouldRegisterUser()
    {
        var passwordHash = "abcdef";
        var token = "123456";
        var userGuid = new Guid();
        _userService.RegisterUserAsync(Arg.Any<User>(), passwordHash, token, userGuid)
                    .Returns(Task.FromResult(IdentityResult.Success));
        var request = new RegisterRequestModel
        {
            Name = "Example User",
            Email = "user@example.com",
            MasterPasswordHash = passwordHash,
            MasterPasswordHint = "example",
            Token = token,
            OrganizationUserId = userGuid
        };

        await _sut.PostRegister(request);

        await _userService.Received(1).RegisterUserAsync(Arg.Any<User>(), passwordHash, token, userGuid);
    }

    [Fact]
    public async Task PostRegister_WhenUserServiceFails_ShouldThrowBadRequestException()
    {
        var passwordHash = "abcdef";
        var token = "123456";
        var userGuid = new Guid();
        _userService.RegisterUserAsync(Arg.Any<User>(), passwordHash, token, userGuid)
                    .Returns(Task.FromResult(IdentityResult.Failed()));
        var request = new RegisterRequestModel
        {
            Name = "Example User",
            Email = "user@example.com",
            MasterPasswordHash = passwordHash,
            MasterPasswordHint = "example",
            Token = token,
            OrganizationUserId = userGuid
        };

        await Assert.ThrowsAsync<BadRequestException>(() => _sut.PostRegister(request));
    }
}
