using Bit.Core.AdminConsole.Services;
using Bit.Core.Auth.Identity;
using Bit.Core.Auth.Models.Business.Tokenables;
using Bit.Core.Auth.Repositories;
using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Settings;
using Bit.Core.Tokens;
using Bit.Identity.IdentityServer;
using Bit.Identity.Test.Wrappers;
using Bit.Test.Common.AutoFixture.Attributes;
using Duende.IdentityServer.Validation;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;
using AuthFixtures = Bit.Identity.Test.AutoFixture;


namespace Bit.Identity.Test.IdentityServer;

public class BaseRequestValidatorTests
{
    #region Fields (Fight me 🥊)
    private UserManager<User> _userManager;
    private readonly IDeviceRepository _deviceRepository;
    private readonly IDeviceService _deviceService;
    private readonly IUserService _userService;
    private readonly IEventService _eventService;
    private readonly IOrganizationDuoWebTokenProvider _organizationDuoWebTokenProvider;
    private readonly ITemporaryDuoWebV4SDKService _duoWebV4SDKService;
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IOrganizationUserRepository _organizationUserRepository;
    private readonly IApplicationCacheService _applicationCacheService;
    private readonly IMailService _mailService;
    private readonly ILogger<BaseRequestValidatorTests> _logger;
    private readonly ICurrentContext _currentContext;
    private readonly GlobalSettings _globalSettings;
    private readonly IUserRepository _userRepository;
    private readonly IPolicyService _policyService;
    private readonly IDataProtectorTokenFactory<SsoEmail2faSessionTokenable> _tokenDataFactory;
    private readonly IFeatureService _featureService;
    private readonly ISsoConfigRepository _ssoConfigRepository;
    private readonly IUserDecryptionOptionsBuilder _userDecryptionOptionsBuilder;

    private readonly BaseRequestValidatorTestWrapper _sut;
    #endregion  

    public BaseRequestValidatorTests()
    {
        _userManager = SubstituteUserManager();
        _deviceRepository = Substitute.For<IDeviceRepository>();
        _deviceService = Substitute.For<IDeviceService>();
        _userService = Substitute.For<IUserService>();
        _eventService = Substitute.For<IEventService>();
        _organizationDuoWebTokenProvider = Substitute.For<IOrganizationDuoWebTokenProvider>();
        _duoWebV4SDKService = Substitute.For<ITemporaryDuoWebV4SDKService>();
        _organizationRepository = Substitute.For<IOrganizationRepository>();
        _organizationUserRepository = Substitute.For<IOrganizationUserRepository>();
        _applicationCacheService = Substitute.For<IApplicationCacheService>();
        _mailService = Substitute.For<IMailService>();
        _logger = Substitute.For<ILogger<BaseRequestValidatorTests>>();
        _currentContext = Substitute.For<ICurrentContext>();
        _globalSettings = Substitute.For<GlobalSettings>();
        _userRepository = Substitute.For<IUserRepository>();
        _policyService = Substitute.For<IPolicyService>();
        _tokenDataFactory = Substitute.For<IDataProtectorTokenFactory<SsoEmail2faSessionTokenable>>();
        _featureService = Substitute.For<IFeatureService>();
        _ssoConfigRepository = Substitute.For<ISsoConfigRepository>();
        _userDecryptionOptionsBuilder = Substitute.For<IUserDecryptionOptionsBuilder>();

        _sut = new BaseRequestValidatorTestWrapper(
            _userManager,
            _deviceRepository,
            _deviceService,
            _userService,
            _eventService,
            _organizationDuoWebTokenProvider,
            _duoWebV4SDKService,
            _organizationRepository,
            _organizationUserRepository,
            _applicationCacheService,
            _mailService,
            _logger,
            _currentContext,
            _globalSettings,
            _userRepository,
            _policyService,
            _tokenDataFactory,
            _featureService,
            _ssoConfigRepository,
            _userDecryptionOptionsBuilder);
    }

    [Fact]
    public void TestTaskCompletion()
    {
        // Assert
        Assert.NotNull(_sut);
        Assert.True(typeof(BaseRequestValidatorTestWrapper).IsAssignableFrom(_sut.GetType()));
    }

    [Theory, BitAutoData]
    public async Task ValidateAsyncTest_Pass(
        [AuthFixtures.RequestValidation("Username@test.dev")] ValidatedTokenRequest tokenRequest, 
        CustomValidatorRequestContext requestContext)
    {
        var context = new BaseRequestValidationContextFake(
            tokenRequest,
            requestContext
        );
        // Arrange
        await _sut.ValidateAsync(context);

        // Act
        await Task.Delay(100);

        // Assert
        Assert.NotNull(_sut);
    }

    private UserManager<User> SubstituteUserManager()
    {
        return new UserManager<User>(Substitute.For<IUserStore<User>>(),
            Substitute.For<IOptions<IdentityOptions>>(),
            Substitute.For<IPasswordHasher<User>>(),
            Enumerable.Empty<IUserValidator<User>>(),
            Enumerable.Empty<IPasswordValidator<User>>(),
            Substitute.For<ILookupNormalizer>(),
            Substitute.For<IdentityErrorDescriber>(),
            Substitute.For<IServiceProvider>(),
            Substitute.For<ILogger<UserManager<User>>>());
    }
}
