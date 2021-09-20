using Bit.Core.Context;
using Bit.Core.Models.Data;
using Bit.Core.Models.Table;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Settings;
using Bit.Core.Test.AutoFixture;
using Fido2NetLib;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using System.Collections.Generic;
using System.Linq;
using System;
using Xunit;
using Bit.Core.Test.AutoFixture.Attributes;

namespace Bit.Core.Test.Services
{
    public class UserServiceTests
    {
        private readonly UserService _sut;

        private readonly IUserRepository _userRepository;
        private readonly ICipherRepository _cipherRepository;
        private readonly IOrganizationUserRepository _organizationUserRepository;
        private readonly IOrganizationRepository _organizationRepository;
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
        private readonly IFido2 _fido2;
        private readonly CurrentContext _currentContext;
        private readonly GlobalSettings _globalSettings;
        private readonly IOrganizationService _organizationService;
        private readonly IProviderUserRepository _providerUserRepository;

        public UserServiceTests()
        {
            _userRepository = Substitute.For<IUserRepository>();
            _cipherRepository = Substitute.For<ICipherRepository>();
            _organizationUserRepository = Substitute.For<IOrganizationUserRepository>();
            _organizationRepository = Substitute.For<IOrganizationRepository>();
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
            _fido2 = Substitute.For<IFido2>();
            _currentContext = new CurrentContext(null);
            _globalSettings = new GlobalSettings();
            _organizationService = Substitute.For<IOrganizationService>();
            _providerUserRepository = Substitute.For<IProviderUserRepository>();

            _sut = new UserService(
                _userRepository,
                _cipherRepository,
                _organizationUserRepository,
                _organizationRepository,
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
                _fido2,
                _currentContext,
                _globalSettings,
                _organizationService,
                _providerUserRepository
            );
        }

        [Theory, CustomAutoData(typeof(SutProviderCustomization))]
        public async void DeleteAsync_PotentialOrphanedOrg_OneOwnedOrg_Throws(User user, OrganizationUserOrganizationDetails org,
            SutProvider<UserService> sutProvider)
        {
            var organizationUserRepo = sutProvider.GetDependency<IOrganizationUserRepository>();

            // user is the owner of one organization
            organizationUserRepo.GetCountByOnlyOwnerAsync(user.Id).Returns(1);

            // user is only in one organization at all
            organizationUserRepo.GetManyDetailsByUserAsync(user.Id, Enums.OrganizationUserStatusType.Confirmed).Returns(
                new OrganizationUserOrganizationDetails[] { org }
            );

            // there is still somebody else in the org
            organizationUserRepo.GetCountByOrganizationIdAsync(org.OrganizationId).Returns(2); 

            var result = await sutProvider.Sut.DeleteAsync(user);
            Assert.True(result.Errors.Any(error => error.Description.Contains("organization")));
        }

        [Theory, CustomAutoData(typeof(SutProviderCustomization))]
        public async void DeleteAsync_PotentialOrphanedOrg_MultipleOwnedOrgs_Throws(User user, ICollection<OrganizationUserOrganizationDetails> orgs,
            SutProvider<UserService> sutProvider)
        {
            var organizationUserRepo = sutProvider.GetDependency<IOrganizationUserRepository>();

            // user is the owner of multiple organizations
            organizationUserRepo.GetCountByOnlyOwnerAsync(user.Id).Returns(2);

            var result = await sutProvider.Sut.DeleteAsync(user);
            Assert.True(result.Errors.Any(error => error.Description.Contains("organization")));
        }

        [Theory, CustomAutoData(typeof(SutProviderCustomization))]
        public async void DeleteAsync_PotentialOrphanedProvider_Throws(User user, SutProvider<UserService> sutProvider)
        {
            // Skipping previous validation
            var organizationUserRepo = sutProvider.GetDependency<IOrganizationUserRepository>();
            organizationUserRepo.GetCountByOnlyOwnerAsync(user.Id).Returns(0);

            // User is the only owner of at least one provider
            var providerUserRepo = sutProvider.GetDependency<IProviderUserRepository>();
            providerUserRepo.GetCountByOnlyOwnerAsync(user.Id).Returns(1);

            var result = await sutProvider.Sut.DeleteAsync(user);
            Assert.True(result.Errors.Any(error => error.Description.Contains("provider")));
        }

        [Theory, CustomAutoData(typeof(SutProviderCustomization))]
        public async void DeleteAsync_Works(User user, SutProvider<UserService> sutProvider)
        {
            // Skipping previous validations
            var organizationUserRepo = sutProvider.GetDependency<IOrganizationUserRepository>();
            var providerUserRepo = sutProvider.GetDependency<IProviderUserRepository>();
            organizationUserRepo.GetCountByOnlyOwnerAsync(user.Id).Returns(0);
            providerUserRepo.GetCountByOnlyOwnerAsync(user.Id).Returns(0);
            //

            var result = await sutProvider.Sut.DeleteAsync(user);
            Assert.True(!result.Errors.Any());
        }
    }
}
