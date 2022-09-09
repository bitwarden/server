using NSubstitute;
using Xunit;
using System.Security.Claims;
using Microsoft.Extensions.Logging;
using Bit.Api.Controllers;
using Bit.Core.Context;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Settings;
using AutoFixture.Xunit2;
using Bit.Api.Models.Request;
using Core.Models.Data;

namespace Bit.Api.Test.Controllers
{
    public class CiphersControllerTests : IDisposable
    {
        private readonly GlobalSettings _globalSettings;
        private readonly ICurrentContext _currentContext;
        private readonly ICipherRepository _cipherRepository;
        private readonly ICollectionCipherRepository _collectionCipherRepository;
        private readonly ICipherService _cipherService;
        private readonly IUserService _userService;
        private readonly ILogger<CiphersController> _logger;
        private readonly IAttachmentStorageService _attachmentStorageService;
        private readonly IProviderService _providerService;
        private readonly CiphersController _ciphersController;

        public CiphersControllerTests()
        {
            _currentContext = Substitute.For<ICurrentContext>();
            _globalSettings = Substitute.For<GlobalSettings>();
            _cipherRepository = Substitute.For<ICipherRepository>();
            _collectionCipherRepository = Substitute.For<ICollectionCipherRepository>();
            _userService = Substitute.For<IUserService>();
            _cipherService = Substitute.For<ICipherService>();
            _logger = Substitute.For<ILogger<CiphersController>>();
            _attachmentStorageService = Substitute.For<IAttachmentStorageService>();
            _providerService = Substitute.For<IProviderService>();

            _ciphersController = new CiphersController(_cipherRepository, _collectionCipherRepository, _cipherService,
                _userService, _attachmentStorageService, _providerService, _currentContext, _logger, _globalSettings);
        }

        public void Dispose()
        {
            _ciphersController?.Dispose();
        }

        [Theory, AutoData]
        public async Task PutPartialShouldReturnCipherWithGivenFolderAndFavoriteValues(Guid userId, Guid folderId)
        {
            var cipherIdString = Guid.NewGuid().ToString();
            var isFavorite = true;

            _userService.GetProperUserId(Arg.Any<ClaimsPrincipal>()).Returns(userId);

            var cipherDetails = new CipherDetails
            {
                Favorite = isFavorite, 
                FolderId = folderId, 
                Type = Core.Enums.CipherType.SecureNote,
                Data = "{}"
            };
            _cipherRepository.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<Guid>()).Returns(Task.FromResult(cipherDetails));

            var result = await _ciphersController.PutPartial(cipherIdString, new CipherPartialRequestModel{Favorite = isFavorite, FolderId = folderId.ToString()});

            Assert.Equal(folderId.ToString(), result.FolderId);
            Assert.Equal(isFavorite, result.Favorite);
        }
    }
}
