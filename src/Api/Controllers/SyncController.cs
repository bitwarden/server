using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Bit.Core.Models.Api;
using Bit.Core.Services;
using Bit.Core.Repositories;
using Bit.Core;
using Bit.Core.Enums;
using Bit.Core.Exceptions;

namespace Bit.Api.Controllers
{
    [Route("sync")]
    [Authorize("Application")]
    public class SyncController : Controller
    {
        private readonly IUserService _userService;
        private readonly IFolderRepository _folderRepository;
        private readonly ICipherRepository _cipherRepository;
        private readonly IOrganizationUserRepository _organizationUserRepository;
        private readonly GlobalSettings _globalSettings;

        public SyncController(
            IUserService userService,
            IFolderRepository folderRepository,
            ICipherRepository cipherRepository,
            IOrganizationUserRepository organizationUserRepository,
            GlobalSettings globalSettings)
        {
            _userService = userService;
            _folderRepository = folderRepository;
            _cipherRepository = cipherRepository;
            _organizationUserRepository = organizationUserRepository;
            _globalSettings = globalSettings;
        }

        [HttpGet("")]
        public async Task<SyncResponseModel> Get()
        {
            var user = await _userService.GetUserByPrincipalAsync(User);
            if(user == null)
            {
                throw new BadRequestException("User not found.");
            }

            var organizationUserDetails = await _organizationUserRepository.GetManyDetailsByUserAsync(user.Id,
                OrganizationUserStatusType.Confirmed);
            var folders = await _folderRepository.GetManyByUserIdAsync(user.Id);
            var ciphers = await _cipherRepository.GetManyByUserIdAsync(user.Id);
            var response = new SyncResponseModel(_globalSettings, user, organizationUserDetails, folders, ciphers);
            return response;
        }
    }
}
