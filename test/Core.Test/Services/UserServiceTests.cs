using System;
using System.Collections.Generic;
using Bit.Core.Models.Table;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Settings;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;
using Bit.Core.Context;

namespace Bit.Core.Test.Services
{
    public class UserServiceTests
    {
        private readonly UserService _sut;

        private readonly IUserRepository _userRepository;
        private readonly ICipherRepository _cipherRepository;
        private readonly IOrganizationUserRepository _organizationUserRepository;
        private readonly IOrganizationRepository _organizationRepository;
        private readonly IU2fRepository _u2fRepository;
        private readonly IMailService _mailService;
        private readonly IPushNotificationService _pushService;
        private readonly IUserStore<User> _userStore;
        private readonly IOptions<IdentityOptions> _optionsAccessor;
        private readonly IPasswordHasher<User> _passwordHasher;
        private readonly IEnumerable<IUserValidator<User>> _userValidators;
        private readonly IEnumerable<IPasswordValidator<User>> _passwordValidators;
        private readonly ILookupNormalizer _keyNormalizer;
        private readonly IdentityErrorDescriber _errors;
        private readonly IServiceProvider _services;
        private readonly ILogger<UserManager<User>> _logger;
        private readonly ILicensingService _licenseService;
        private readonly IEventService _eventService;
        private readonly IApplicationCacheService _applicationCacheService;
        private readonly IDataProtectionProvider _dataProtectionProvider;
        private readonly IPaymentService _paymentService;
        private readonly IPolicyRepository _policyRepository;
        private readonly IReferenceEventService _referenceEventService;
        private readonly CurrentContext _currentContext;
        private readonly GlobalSettings _globalSettings;
        private readonly IOrganizationService _organizationService;

        public UserServiceTests()
        {
            _userRepository = Substitute.For<IUserRepository>();
            _cipherRepository = Substitute.For<ICipherRepository>();
            _organizationUserRepository = Substitute.For<IOrganizationUserRepository>();
            _organizationRepository = Substitute.For<IOrganizationRepository>();
            _u2fRepository = Substitute.For<IU2fRepository>();
            _mailService = Substitute.For<IMailService>();
            _pushService = Substitute.For<IPushNotificationService>();
            _userStore = Substitute.For<IUserStore<User>>();
            _optionsAccessor = Substitute.For<IOptions<IdentityOptions>>();
            _passwordHasher = Substitute.For<IPasswordHasher<User>>();
            _userValidators = new List<IUserValidator<User>>();
            _passwordValidators = new List<IPasswordValidator<User>>();
            _keyNormalizer = Substitute.For<ILookupNormalizer>();
            _errors = new IdentityErrorDescriber();
            _services = Substitute.For<IServiceProvider>();
            _logger = Substitute.For<ILogger<UserManager<User>>>();
            _licenseService = Substitute.For<ILicensingService>();
            _eventService = Substitute.For<IEventService>();
            _applicationCacheService = Substitute.For<IApplicationCacheService>();
            _dataProtectionProvider = Substitute.For<IDataProtectionProvider>();
            _paymentService = Substitute.For<IPaymentService>();
            _policyRepository = Substitute.For<IPolicyRepository>();
            _referenceEventService = Substitute.For<IReferenceEventService>();
            _currentContext = new CurrentContext();
            _globalSettings = new GlobalSettings();
            _organizationService = Substitute.For<IOrganizationService>();

            _sut = new UserService(
                _userRepository,
                _cipherRepository,
                _organizationUserRepository,
                _organizationRepository,
                _u2fRepository,
                _mailService,
                _pushService,
                _userStore,
                _optionsAccessor,
                _passwordHasher,
                _userValidators,
                _passwordValidators,
                _keyNormalizer,
                _errors,
                _services,
                _logger,
                _licenseService,
                _eventService,
                _applicationCacheService,
                _dataProtectionProvider,
                _paymentService,
                _policyRepository,
                _referenceEventService,
                _currentContext,
                _globalSettings,
                _organizationService
            );
        }

        // Remove this test when we add actual tests. It only proves that
        // we've properly constructed the system under test.
        [Fact]
        public void ServiceExists()
        {
            Assert.NotNull(_sut);
        }
    }
}
