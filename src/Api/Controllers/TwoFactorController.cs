using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Bit.Core.Models.Api;
using Bit.Core.Exceptions;
using Bit.Core.Services;
using Microsoft.AspNetCore.Identity;
using Bit.Core.Models.Table;
using Bit.Core.Enums;
using System.Linq;

namespace Bit.Api.Controllers
{
    [Route("two-factor")]
    [Authorize("Application")]
    public class TwoFactorController : Controller
    {
        private readonly IUserService _userService;
        private readonly UserManager<User> _userManager;

        public TwoFactorController(
            IUserService userService,
            UserManager<User> userManager)
        {
            _userService = userService;
            _userManager = userManager;
        }

        [HttpGet("")]
        public async Task<ListResponseModel<TwoFactorProviderResponseModel>> Get()
        {
            var user = await _userService.GetUserByPrincipalAsync(User);
            if(user == null)
            {
                throw new UnauthorizedAccessException();
            }

            var providers = user.GetTwoFactorProviders().Select(p => new TwoFactorProviderResponseModel(p.Key, p.Value));
            return new ListResponseModel<TwoFactorProviderResponseModel>(providers);
        }

        [HttpPost("get-authenticator")]
        public async Task<TwoFactorAuthenticatorResponseModel> GetAuthenticator([FromBody]TwoFactorRequestModel model)
        {
            var user = await GetProviderAsync(model, TwoFactorProviderType.Authenticator);
            var response = new TwoFactorAuthenticatorResponseModel(user);
            return response;
        }

        [HttpPut("authenticator")]
        [HttpPost("authenticator")]
        public async Task<TwoFactorAuthenticatorResponseModel> PutAuthenticator(
            [FromBody]UpdateTwoFactorAuthenticatorRequestModel model)
        {
            var user = await _userService.GetUserByPrincipalAsync(User);
            if(user == null)
            {
                throw new UnauthorizedAccessException();
            }

            if(!await _userManager.CheckPasswordAsync(user, model.MasterPasswordHash))
            {
                await Task.Delay(2000);
                throw new BadRequestException("MasterPasswordHash", "Invalid password.");
            }

            if(!await _userManager.VerifyTwoFactorTokenAsync(user, TwoFactorProviderType.Authenticator.ToString(), model.Token))
            {
                await Task.Delay(2000);
                throw new BadRequestException("Token", "Invalid token.");
            }

            await _userService.UpdateTwoFactorProviderAsync(user, TwoFactorProviderType.Authenticator);
            var response = new TwoFactorAuthenticatorResponseModel(user);
            return response;
        }

        [HttpPost("get-email")]
        public async Task<TwoFactorEmailResponseModel> GetEmail([FromBody]TwoFactorRequestModel model)
        {
            var user = await GetProviderAsync(model, TwoFactorProviderType.Email);
            var response = new TwoFactorEmailResponseModel(user);
            return response;
        }

        [HttpPut("email")]
        [HttpPost("email")]
        public async Task<TwoFactorEmailResponseModel> PutEmail([FromBody]UpdateTwoFactorEmailRequestModel model)
        {
            var user = await _userService.GetUserByPrincipalAsync(User);
            if(user == null)
            {
                throw new UnauthorizedAccessException();
            }

            if(!await _userManager.CheckPasswordAsync(user, model.MasterPasswordHash))
            {
                await Task.Delay(2000);
                throw new BadRequestException("MasterPasswordHash", "Invalid password.");
            }

            if(!await _userManager.VerifyTwoFactorTokenAsync(user, TwoFactorProviderType.Email.ToString(), model.Token))
            {
                await Task.Delay(2000);
                throw new BadRequestException("Token", "Invalid token.");
            }

            await _userService.UpdateTwoFactorProviderAsync(user, TwoFactorProviderType.Email);

            var response = new TwoFactorEmailResponseModel(user);
            return response;
        }

        [HttpPut("disable")]
        [HttpPost("disable")]
        public async Task<TwoFactorEmailResponseModel> PutDisable([FromBody]TwoFactorProviderRequestModel model)
        {
            var user = await _userService.GetUserByPrincipalAsync(User);
            if(user == null)
            {
                throw new UnauthorizedAccessException();
            }

            if(!await _userManager.CheckPasswordAsync(user, model.MasterPasswordHash))
            {
                await Task.Delay(2000);
                throw new BadRequestException("MasterPasswordHash", "Invalid password.");
            }

            await _userService.DisableTwoFactorProviderAsync(user, model.Type.Value);

            var response = new TwoFactorEmailResponseModel(user);
            return response;
        }

        [HttpPost("recover")]
        [AllowAnonymous]
        public async Task PostTwoFactorRecover([FromBody]TwoFactorRecoveryRequestModel model)
        {
            if(!await _userService.RecoverTwoFactorAsync(model.Email, model.MasterPasswordHash, model.RecoveryCode))
            {
                await Task.Delay(2000);
                throw new BadRequestException(string.Empty, "Invalid information. Try again.");
            }
        }

        private async Task<User> GetProviderAsync(TwoFactorRequestModel model, TwoFactorProviderType type)
        {
            var user = await _userService.GetUserByPrincipalAsync(User);
            if(user == null)
            {
                throw new UnauthorizedAccessException();
            }

            if(!await _userManager.CheckPasswordAsync(user, model.MasterPasswordHash))
            {
                await Task.Delay(2000);
                throw new BadRequestException("MasterPasswordHash", "Invalid password.");
            }

            await _userService.SetupTwoFactorAsync(user, type);
            return user;
        }
    }
}
