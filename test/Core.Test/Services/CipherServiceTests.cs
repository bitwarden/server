using System;
using Bit.Core.Repositories;
using Bit.Core.Services;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.Services
{
    public class CipherServiceTests
    {
        private readonly CipherService _sut;

        private readonly ICipherRepository _cipherRepository;
        private readonly IFolderRepository _folderRepository;
        private readonly ICollectionRepository _collectionRepository;
        private readonly IUserRepository _userRepository;
        private readonly IOrganizationRepository _organizationRepository;
        private readonly IOrganizationUserRepository _organizationUserRepository;
        private readonly ICollectionCipherRepository _collectionCipherRepository;
        private readonly IPushNotificationService _pushService;
        private readonly IAttachmentStorageService _attachmentStorageService;
        private readonly IEventService _eventService;
        private readonly IUserService _userService;
        private readonly GlobalSettings _globalSettings;

        public CipherServiceTests()
        {
            _cipherRepository = Substitute.For<ICipherRepository>();
            _folderRepository = Substitute.For<IFolderRepository>();
            _collectionRepository = Substitute.For<ICollectionRepository>();
            _userRepository = Substitute.For<IUserRepository>();
            _organizationRepository = Substitute.For<IOrganizationRepository>();
            _organizationUserRepository = Substitute.For<IOrganizationUserRepository>();
            _collectionCipherRepository = Substitute.For<ICollectionCipherRepository>();
            _pushService = Substitute.For<IPushNotificationService>();
            _attachmentStorageService = Substitute.For<IAttachmentStorageService>();
            _eventService = Substitute.For<IEventService>();
            _userService = Substitute.For<IUserService>();
            _globalSettings = new GlobalSettings();

            _sut = new CipherService(
                _cipherRepository,
                _folderRepository,
                _collectionRepository,
                _userRepository,
                _organizationRepository,
                _organizationUserRepository,
                _collectionCipherRepository,
                _pushService,
                _attachmentStorageService,
                _eventService,
                _userService,
                _globalSettings
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
