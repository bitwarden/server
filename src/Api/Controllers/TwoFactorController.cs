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
            var user = await CheckPasswordAsync(model.MasterPasswordHash);
            var response = new TwoFactorAuthenticatorResponseModel(user);
            return response;
        }

        [HttpPut("authenticator")]
        [HttpPost("authenticator")]
        public async Task<TwoFactorAuthenticatorResponseModel> PutAuthenticator(
            [FromBody]UpdateTwoFactorAuthenticatorRequestModel model)
        {
            var user = await CheckPasswordAsync(model.MasterPasswordHash);
            model.ToUser(user);

            if(!await _userManager.VerifyTwoFactorTokenAsync(user, TwoFactorProviderType.Authenticator.ToString(), model.Token))
            {
                await Task.Delay(2000);
                throw new BadRequestException("Token", "Invalid token.");
            }

            await _userService.UpdateTwoFactorProviderAsync(user, TwoFactorProviderType.Authenticator);
            var response = new TwoFactorAuthenticatorResponseModel(user);
            return response;
        }

        [HttpPost("get-yubikey")]
        public async Task<TwoFactorYubiKeyResponseModel> GetYubiKey([FromBody]TwoFactorRequestModel model)
        {
            var user = await CheckPasswordAsync(model.MasterPasswordHash);
            var response = new TwoFactorYubiKeyResponseModel(user);
            return response;
        }

        [HttpPut("yubikey")]
        [HttpPost("yubikey")]
        public async Task<TwoFactorYubiKeyResponseModel> PutYubiKey([FromBody]UpdateTwoFactorYubicoOtpRequestModel model)
        {
            var user = await CheckPasswordAsync(model.MasterPasswordHash);
            model.ToUser(user);

            await ValidateYubiKeyAsync(user, nameof(model.Key1), model.Key1);
            await ValidateYubiKeyAsync(user, nameof(model.Key2), model.Key2);
            await ValidateYubiKeyAsync(user, nameof(model.Key3), model.Key3);
            await ValidateYubiKeyAsync(user, nameof(model.Key4), model.Key4);
            await ValidateYubiKeyAsync(user, nameof(model.Key5), model.Key5);

            await _userService.UpdateTwoFactorProviderAsync(user, TwoFactorProviderType.YubiKey);
            var response = new TwoFactorYubiKeyResponseModel(user);
            return response;
        }

        [HttpPost("get-duo")]
        public async Task<TwoFactorDuoResponseModel> GetDuo([FromBody]TwoFactorRequestModel model)
        {
            var user = await CheckPasswordAsync(model.MasterPasswordHash);
            var response = new TwoFactorDuoResponseModel(user);
            return response;
        }

        [HttpPut("duo")]
        [HttpPost("duo")]
        public async Task<TwoFactorDuoResponseModel> PutDuo([FromBody]UpdateTwoFactorDuoRequestModel model)
        {
            var user = await CheckPasswordAsync(model.MasterPasswordHash);
            model.ToUser(user);
            await _userService.UpdateTwoFactorProviderAsync(user, TwoFactorProviderType.Duo);
            var response = new TwoFactorDuoResponseModel(user);
            return response;
        }

        public async Task ValidateYubiKeyAsync(User user, string name, string value)
        {
            if(string.IsNullOrWhiteSpace(value) || value.Length == 12)
            {
                return;
            }

            if(!await _userManager.VerifyTwoFactorTokenAsync(user, TwoFactorProviderType.YubiKey.ToString(), value))
            {
                await Task.Delay(2000);
                throw new BadRequestException(name, $"{name} is invalid.");
            }
            else
            {
                await Task.Delay(500);
            }
        }

        [HttpPost("get-email")]
        public async Task<TwoFactorEmailResponseModel> GetEmail([FromBody]TwoFactorRequestModel model)
        {
            var user = await CheckPasswordAsync(model.MasterPasswordHash);
            var response = new TwoFactorEmailResponseModel(user);
            return response;
        }

        [HttpPost("send-email")]
        public async Task SendEmail([FromBody]TwoFactorEmailRequestModel model)
        {
            var user = await CheckPasswordAsync(model.MasterPasswordHash);
            model.ToUser(user);
            await _userService.SendTwoFactorEmailAsync(user);
        }

        [HttpPut("email")]
        [HttpPost("email")]
        public async Task<TwoFactorEmailResponseModel> PutEmail([FromBody]UpdateTwoFactorEmailRequestModel model)
        {
            var user = await CheckPasswordAsync(model.MasterPasswordHash);
            model.ToUser(user);

            if(!await _userService.VerifyTwoFactorEmailAsync(user, model.Token))
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
        public async Task<TwoFactorProviderResponseModel> PutDisable([FromBody]TwoFactorProviderRequestModel model)
        {
            var user = await CheckPasswordAsync(model.MasterPasswordHash);
            await _userService.DisableTwoFactorProviderAsync(user, model.Type.Value);
            var response = new TwoFactorProviderResponseModel(model.Type.Value, user);
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

        private async Task<User> CheckPasswordAsync(string masterPasswordHash)
        {
            var user = await _userService.GetUserByPrincipalAsync(User);
            if(user == null)
            {
                throw new UnauthorizedAccessException();
            }

            if(!await _userManager.CheckPasswordAsync(user, masterPasswordHash))
            {
                await Task.Delay(2000);
                throw new BadRequestException("MasterPasswordHash", "Invalid password.");
            }

            return user;
        }
    }
}
