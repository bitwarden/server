using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Bit.Api.Models;
using Bit.Core.Exceptions;
using Bit.Core.Services;
using Microsoft.AspNetCore.Identity;
using Bit.Core.Domains;
using Bit.Core.Enums;
using Bit.Core;
using System.Security.Claims;
using System.Linq;

namespace Bit.Api.Controllers
{
    [Route("accounts")]
    [Authorize("Application")]
    public class AccountsController : Controller
    {
        private readonly IUserService _userService;
        private readonly ICipherService _cipherService;
        private readonly UserManager<User> _userManager;
        private readonly CurrentContext _currentContext;

        public AccountsController(
            IUserService userService,
            ICipherService cipherService,
            UserManager<User> userManager,
            CurrentContext currentContext)
        {
            _userService = userService;
            _cipherService = cipherService;
            _userManager = userManager;
            _currentContext = currentContext;
        }

        [HttpPost("register")]
        [AllowAnonymous]
        public async Task PostRegister([FromBody]RegisterRequestModel model)
        {
            var result = await _userService.RegisterUserAsync(model.ToUser(), model.MasterPasswordHash);
            if(result.Succeeded)
            {
                return;
            }

            foreach(var error in result.Errors.Where(e => e.Code != "DuplicateUserName"))
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }

            await Task.Delay(2000);
            throw new BadRequestException(ModelState);
        }

        [HttpPost("password-hint")]
        [AllowAnonymous]
        public async Task PostPasswordHint([FromBody]PasswordHintRequestModel model)
        {
            await _userService.SendMasterPasswordHintAsync(model.Email);
        }

        [HttpPost("email-token")]
        public async Task PostEmailToken([FromBody]EmailTokenRequestModel model)
        {
            if(!await _userManager.CheckPasswordAsync(_currentContext.User, model.MasterPasswordHash))
            {
                await Task.Delay(2000);
                throw new BadRequestException("MasterPasswordHash", "Invalid password.");
            }

            await _userService.InitiateEmailChangeAsync(_currentContext.User, model.NewEmail);
        }

        [HttpPut("email")]
        [HttpPost("email")]
        public async Task PutEmail([FromBody]EmailRequestModel model)
        {
            // NOTE: It is assumed that the eventual repository call will make sure the updated
            // ciphers belong to user making this call. Therefore, no check is done here.
            var ciphers = model.Ciphers.Select(c => c.ToCipher(_userManager.GetUserId(User)));

            var result = await _userService.ChangeEmailAsync(
                _currentContext.User,
                model.MasterPasswordHash,
                model.NewEmail,
                model.NewMasterPasswordHash,
                model.Token,
                ciphers);

            if(result.Succeeded)
            {
                return;
            }

            foreach(var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }

            await Task.Delay(2000);
            throw new BadRequestException(ModelState);
        }

        [HttpPut("password")]
        [HttpPost("password")]
        public async Task PutPassword([FromBody]PasswordRequestModel model)
        {
            // NOTE: It is assumed that the eventual repository call will make sure the updated
            // ciphers belong to user making this call. Therefore, no check is done here.
            var ciphers = model.Ciphers.Select(c => c.ToCipher(_userManager.GetUserId(User)));

            var result = await _userService.ChangePasswordAsync(
                _currentContext.User,
                model.MasterPasswordHash,
                model.NewMasterPasswordHash,
                ciphers);

            if(result.Succeeded)
            {
                return;
            }

            foreach(var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }

            await Task.Delay(2000);
            throw new BadRequestException(ModelState);
        }

        [HttpPut("security-stamp")]
        [HttpPost("security-stamp")]
        public async Task PutSecurityStamp([FromBody]SecurityStampRequestModel model)
        {
            var result = await _userService.RefreshSecurityStampAsync(_currentContext.User, model.MasterPasswordHash);
            if(result.Succeeded)
            {
                return;
            }

            foreach(var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }

            await Task.Delay(2000);
            throw new BadRequestException(ModelState);
        }

        [HttpGet("profile")]
        public Task<ProfileResponseModel> GetProfile()
        {
            var response = new ProfileResponseModel(_currentContext.User);
            return Task.FromResult(response);
        }

        [HttpGet("domains")]
        public Task<DomainsResponseModel> GetDomains()
        {
            var response = new DomainsResponseModel(_currentContext.User);
            return Task.FromResult(response);
        }

        [HttpPut("profile")]
        [HttpPost("profile")]
        public async Task<ProfileResponseModel> PutProfile([FromBody]UpdateProfileRequestModel model)
        {
            await _userService.SaveUserAsync(model.ToUser(_currentContext.User));

            var response = new ProfileResponseModel(_currentContext.User);
            return response;
        }                      

        [HttpPut("domains")]
        [HttpPost("domains")]
        public async Task<DomainsResponseModel> PutDomains([FromBody]UpdateDomainsRequestModel model)
        {
            await _userService.SaveUserAsync(model.ToUser(_currentContext.User));

            var response = new DomainsResponseModel(_currentContext.User);
            return response;
        }

        [HttpGet("two-factor")]
        public async Task<TwoFactorResponseModel> GetTwoFactor(string masterPasswordHash, TwoFactorProviderType provider)
        {
            var user = _currentContext.User;
            if(!await _userManager.CheckPasswordAsync(user, masterPasswordHash))
            {
                await Task.Delay(2000);
                throw new BadRequestException("MasterPasswordHash", "Invalid password.");
            }

            await _userService.GetTwoFactorAsync(user, provider);

            var response = new TwoFactorResponseModel(user);
            return response;
        }

        [HttpPut("two-factor")]
        [HttpPost("two-factor")]
        public async Task<TwoFactorResponseModel> PutTwoFactor([FromBody]UpdateTwoFactorRequestModel model)
        {
            var user = _currentContext.User;
            if(!await _userManager.CheckPasswordAsync(user, model.MasterPasswordHash))
            {
                await Task.Delay(2000);
                throw new BadRequestException("MasterPasswordHash", "Invalid password.");
            }

            if(!await _userManager.VerifyTwoFactorTokenAsync(user, "Authenticator", model.Token))
            {
                await Task.Delay(2000);
                throw new BadRequestException("Token", "Invalid token.");
            }

            user.TwoFactorProvider = TwoFactorProviderType.Authenticator;
            user.TwoFactorEnabled = model.Enabled.Value;
            user.TwoFactorRecoveryCode = user.TwoFactorEnabled ? Guid.NewGuid().ToString("N") : null;
            await _userService.SaveUserAsync(user);

            var response = new TwoFactorResponseModel(user);
            return response;
        }

        [HttpPost("two-factor-recover")]
        [AllowAnonymous]
        public async Task PostTwoFactorRecover([FromBody]RecoverTwoFactorRequestModel model)
        {
            if(!await _userService.RecoverTwoFactorAsync(model.Email, model.MasterPasswordHash, model.RecoveryCode))
            {
                await Task.Delay(2000);
                throw new BadRequestException(string.Empty, "Invalid information. Try again.");
            }
        }

        [HttpPut("two-factor-regenerate")]
        [HttpPost("two-factor-regenerate")]
        public async Task<TwoFactorResponseModel> PutTwoFactorRegenerate([FromBody]RegenerateTwoFactorRequestModel model)
        {
            var user = _currentContext.User;
            if(!await _userManager.CheckPasswordAsync(user, model.MasterPasswordHash))
            {
                await Task.Delay(2000);
                throw new BadRequestException("MasterPasswordHash", "Invalid password.");
            }

            if(!await _userManager.VerifyTwoFactorTokenAsync(user, "Authenticator", model.Token))
            {
                await Task.Delay(2000);
                throw new BadRequestException("Token", "Invalid token.");
            }

            if(user.TwoFactorEnabled)
            {
                user.TwoFactorRecoveryCode = Guid.NewGuid().ToString("N");
                await _userService.SaveUserAsync(user);
            }

            var response = new TwoFactorResponseModel(user);
            return response;
        }

        [HttpPost("delete")]
        public async Task PostDelete([FromBody]DeleteAccountRequestModel model)
        {
            var user = _currentContext.User;
            if(!await _userManager.CheckPasswordAsync(user, model.MasterPasswordHash))
            {
                ModelState.AddModelError("MasterPasswordHash", "Invalid password.");
                await Task.Delay(2000);
            }
            else
            {
                var result = await _userService.DeleteAsync(user);
                if(result.Succeeded)
                {
                    return;
                }

                foreach(var error in result.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }
            }

            throw new BadRequestException(ModelState);
        }
    }
}
