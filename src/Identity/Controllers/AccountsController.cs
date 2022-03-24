using System.Linq;
using System.Threading.Tasks;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Models.Api.Request.Accounts;
using Bit.Core.Models.Api.Response.Accounts;
using Bit.Core.Models.Data;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Utilities;
using Bit.SharedWeb.Utilities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Bit.Identity.Controllers
{
    [Route("accounts")]
    [ExceptionHandlerFilter]
    public class AccountsController : Controller
    {
        private readonly ILogger<AccountsController> _logger;
        private readonly IUserRepository _userRepository;
        private readonly IUserService _userService;

        public AccountsController(
            ILogger<AccountsController> logger,
            IUserRepository userRepository,
            IUserService userService)
        {
            _logger = logger;
            _userRepository = userRepository;
            _userService = userService;
        }

        // Moved from API, If you modify this endpoint, please update API as well.
        [HttpPost("register")]
        [CaptchaProtected]
        public async Task PostRegister([FromBody] RegisterRequestModel model)
        {
            var result = await _userService.RegisterUserAsync(model.ToUser(), model.MasterPasswordHash,
                model.Token, model.OrganizationUserId);
            if (result.Succeeded)
            {
                return;
            }

            foreach (var error in result.Errors.Where(e => e.Code != "DuplicateUserName"))
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }

            await Task.Delay(2000);
            throw new BadRequestException(ModelState);
        }

        // Moved from API, If you modify this endpoint, please update API as well.
        [HttpPost("prelogin")]
        public async Task<PreloginResponseModel> PostPrelogin([FromBody] PreloginRequestModel model)
        {
            var kdfInformation = await _userRepository.GetKdfInformationByEmailAsync(model.Email);
            if (kdfInformation == null)
            {
                kdfInformation = new UserKdfInformation
                {
                    Kdf = KdfType.PBKDF2_SHA256,
                    KdfIterations = 100000,
                };
            }
            return new PreloginResponseModel(kdfInformation);
        }
    }
}
