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
using Bit.Core;
using Bit.Core.Repositories;
using Bit.Core.Utilities;
using Bit.Core.Utilities.Duo;

namespace Bit.Api.Controllers
{
    [Route("two-factor")]
    [Authorize("Web")]
    public class TwoFactorController : Controller
    {
        private readonly IUserService _userService;
        private readonly IOrganizationRepository _organizationRepository;
        private readonly IOrganizationService _organizationService;
        private readonly GlobalSettings _globalSettings;
        private readonly UserManager<User> _userManager;
        private readonly CurrentContext _currentContext;

        public TwoFactorController(
            IUserService userService,
            IOrganizationRepository organizationRepository,
            IOrganizationService organizationService,
            GlobalSettings globalSettings,
            UserManager<User> userManager,
            CurrentContext currentContext)
        {
            _userService = userService;
            _organizationRepository = organizationRepository;
            _organizationService = organizationService;
            _globalSettings = globalSettings;
            _userManager = userManager;
            _currentContext = currentContext;
        }

        [HttpGet("")]
        public async Task<ListResponseModel<TwoFactorProviderResponseModel>> Get()
        {
            var user = await _userService.GetUserByPrincipalAsync(User);
            if(user == null)
            {
                throw new UnauthorizedAccessException();
            }

            var providers = user.GetTwoFactorProviders()?.Select(
                p => new TwoFactorProviderResponseModel(p.Key, p.Value));
            return new ListResponseModel<TwoFactorProviderResponseModel>(providers);
        }

        [HttpGet("~/organizations/{id}/two-factor")]
        public async Task<ListResponseModel<TwoFactorProviderResponseModel>> GetOrganization(string id)
        {
            var orgIdGuid = new Guid(id);
            if(!_currentContext.OrganizationAdmin(orgIdGuid))
            {
                throw new NotFoundException();
            }

            var organization = await _organizationRepository.GetByIdAsync(orgIdGuid);
            if(organization == null)
            {
                throw new NotFoundException();
            }

            var providers = organization.GetTwoFactorProviders()?.Select(
                p => new TwoFactorProviderResponseModel(p.Key, p.Value));
            return new ListResponseModel<TwoFactorProviderResponseModel>(providers);
        }

        [HttpPost("get-authenticator")]
        public async Task<TwoFactorAuthenticatorResponseModel> GetAuthenticator([FromBody]TwoFactorRequestModel model)
        {
            var user = await CheckAsync(model.MasterPasswordHash, false);
            var response = new TwoFactorAuthenticatorResponseModel(user);
            return response;
        }

        [HttpPut("authenticator")]
        [HttpPost("authenticator")]
        public async Task<TwoFactorAuthenticatorResponseModel> PutAuthenticator(
            [FromBody]UpdateTwoFactorAuthenticatorRequestModel model)
        {
            var user = await CheckAsync(model.MasterPasswordHash, false);
            model.ToUser(user);

            if(!await _userManager.VerifyTwoFactorTokenAsync(user,
                CoreHelpers.CustomProviderName(TwoFactorProviderType.Authenticator), model.Token))
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
            var user = await CheckAsync(model.MasterPasswordHash, true);
            var response = new TwoFactorYubiKeyResponseModel(user);
            return response;
        }

        [HttpPut("yubikey")]
        [HttpPost("yubikey")]
        public async Task<TwoFactorYubiKeyResponseModel> PutYubiKey([FromBody]UpdateTwoFactorYubicoOtpRequestModel model)
        {
            var user = await CheckAsync(model.MasterPasswordHash, true);
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
            var user = await CheckAsync(model.MasterPasswordHash, true);
            var response = new TwoFactorDuoResponseModel(user);
            return response;
        }

        [HttpPut("duo")]
        [HttpPost("duo")]
        public async Task<TwoFactorDuoResponseModel> PutDuo([FromBody]UpdateTwoFactorDuoRequestModel model)
        {
            var user = await CheckAsync(model.MasterPasswordHash, true);
            try
            {
                var duoApi = new DuoApi(model.IntegrationKey, model.SecretKey, model.Host);
                duoApi.JSONApiCall<object>("GET", "/auth/v2/check");
            }
            catch(DuoException)
            {
                throw new BadRequestException("Duo configuration settings are not valid. Please re-check the Duo Admin panel.");
            }

            model.ToUser(user);
            await _userService.UpdateTwoFactorProviderAsync(user, TwoFactorProviderType.Duo);
            var response = new TwoFactorDuoResponseModel(user);
            return response;
        }

        [HttpPost("~/organizations/{id}/two-factor/get-duo")]
        public async Task<TwoFactorDuoResponseModel> GetOrganizationDuo(string id,
            [FromBody]TwoFactorRequestModel model)
        {
            var user = await CheckAsync(model.MasterPasswordHash, false);

            var orgIdGuid = new Guid(id);
            if(!_currentContext.OrganizationAdmin(orgIdGuid))
            {
                throw new NotFoundException();
            }

            var organization = await _organizationRepository.GetByIdAsync(orgIdGuid);
            if(organization == null)
            {
                throw new NotFoundException();
            }

            var response = new TwoFactorDuoResponseModel(organization);
            return response;
        }

        [HttpPut("~/organizations/{id}/two-factor/duo")]
        [HttpPost("~/organizations/{id}/two-factor/duo")]
        public async Task<TwoFactorDuoResponseModel> PutOrganizationDuo(string id,
            [FromBody]UpdateTwoFactorDuoRequestModel model)
        {
            var user = await CheckAsync(model.MasterPasswordHash, false);

            var orgIdGuid = new Guid(id);
            if(!_currentContext.OrganizationAdmin(orgIdGuid))
            {
                throw new NotFoundException();
            }

            var organization = await _organizationRepository.GetByIdAsync(orgIdGuid);
            if(organization == null)
            {
                throw new NotFoundException();
            }

            try
            {
                var duoApi = new DuoApi(model.IntegrationKey, model.SecretKey, model.Host);
                duoApi.JSONApiCall<object>("GET", "/auth/v2/check");
            }
            catch(DuoException)
            {
                throw new BadRequestException("Duo configuration settings are not valid. Please re-check the Duo Admin panel.");
            }

            model.ToOrganization(organization);
            await _organizationService.UpdateTwoFactorProviderAsync(organization,
                TwoFactorProviderType.OrganizationDuo);
            var response = new TwoFactorDuoResponseModel(organization);
            return response;
        }

        [HttpPost("get-u2f")]
        public async Task<TwoFactorU2fResponseModel> GetU2f([FromBody]TwoFactorRequestModel model)
        {
            var user = await CheckAsync(model.MasterPasswordHash, true);
            var response = new TwoFactorU2fResponseModel(user);
            return response;
        }

        [HttpPost("get-u2f-challenge")]
        public async Task<TwoFactorU2fResponseModel.ChallengeModel> GetU2fChallenge(
            [FromBody]TwoFactorRequestModel model)
        {
            var user = await CheckAsync(model.MasterPasswordHash, true);
            var reg = await _userService.StartU2fRegistrationAsync(user);
            var challenge = new TwoFactorU2fResponseModel.ChallengeModel(user, reg);
            return challenge;
        }

        [HttpPut("u2f")]
        [HttpPost("u2f")]
        public async Task<TwoFactorU2fResponseModel> PutU2f([FromBody]TwoFactorU2fRequestModel model)
        {
            var user = await CheckAsync(model.MasterPasswordHash, true);
            var success = await _userService.CompleteU2fRegistrationAsync(
                user, model.Id.Value, model.Name, model.DeviceResponse);
            if(!success)
            {
                throw new BadRequestException("Unable to complete U2F key registration.");
            }
            var response = new TwoFactorU2fResponseModel(user);
            return response;
        }

        [HttpDelete("u2f")]
        public async Task<TwoFactorU2fResponseModel> DeleteU2f([FromBody]TwoFactorU2fDeleteRequestModel model)
        {
            var user = await CheckAsync(model.MasterPasswordHash, true);
            await _userService.DeleteU2fKeyAsync(user, model.Id.Value);
            var response = new TwoFactorU2fResponseModel(user);
            return response;
        }

        [HttpPost("get-email")]
        public async Task<TwoFactorEmailResponseModel> GetEmail([FromBody]TwoFactorRequestModel model)
        {
            var user = await CheckAsync(model.MasterPasswordHash, false);
            var response = new TwoFactorEmailResponseModel(user);
            return response;
        }

        [HttpPost("send-email")]
        public async Task SendEmail([FromBody]TwoFactorEmailRequestModel model)
        {
            var user = await CheckAsync(model.MasterPasswordHash, false);
            model.ToUser(user);
            await _userService.SendTwoFactorEmailAsync(user);
        }

        [AllowAnonymous]
        [HttpPost("send-email-login")]
        public async Task SendEmailLogin([FromBody]TwoFactorEmailRequestModel model)
        {
            var user = await _userManager.FindByEmailAsync(model.Email.ToLowerInvariant());
            if(user != null)
            {
                if(await _userService.CheckPasswordAsync(user, model.MasterPasswordHash))
                {
                    await _userService.SendTwoFactorEmailAsync(user);
                    return;
                }
            }

            await Task.Delay(2000);
            throw new BadRequestException("Cannot send two-factor email.");
        }

        [HttpPut("email")]
        [HttpPost("email")]
        public async Task<TwoFactorEmailResponseModel> PutEmail([FromBody]UpdateTwoFactorEmailRequestModel model)
        {
            var user = await CheckAsync(model.MasterPasswordHash, false);
            model.ToUser(user);

            if(!await _userManager.VerifyTwoFactorTokenAsync(user,
                CoreHelpers.CustomProviderName(TwoFactorProviderType.Email), model.Token))
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
            var user = await CheckAsync(model.MasterPasswordHash, false);
            await _userService.DisableTwoFactorProviderAsync(user, model.Type.Value);
            var response = new TwoFactorProviderResponseModel(model.Type.Value, user);
            return response;
        }

        [HttpPut("~/organizations/{id}/two-factor/disable")]
        [HttpPost("~/organizations/{id}/two-factor/disable")]
        public async Task<TwoFactorProviderResponseModel> PutOrganizationDisable(string id,
            [FromBody]TwoFactorProviderRequestModel model)
        {
            var user = await CheckAsync(model.MasterPasswordHash, false);

            var orgIdGuid = new Guid(id);
            if(!_currentContext.OrganizationAdmin(orgIdGuid))
            {
                throw new NotFoundException();
            }

            var organization = await _organizationRepository.GetByIdAsync(orgIdGuid);
            if(organization == null)
            {
                throw new NotFoundException();
            }

            await _organizationService.DisableTwoFactorProviderAsync(organization, model.Type.Value);
            var response = new TwoFactorProviderResponseModel(model.Type.Value, organization);
            return response;
        }

        [HttpPost("get-recover")]
        public async Task<TwoFactorRecoverResponseModel> GetRecover([FromBody]TwoFactorRequestModel model)
        {
            var user = await CheckAsync(model.MasterPasswordHash, false);
            var response = new TwoFactorRecoverResponseModel(user);
            return response;
        }

        [HttpPost("recover")]
        [AllowAnonymous]
        public async Task PostRecover([FromBody]TwoFactorRecoveryRequestModel model)
        {
            if(!await _userService.RecoverTwoFactorAsync(model.Email, model.MasterPasswordHash, model.RecoveryCode))
            {
                await Task.Delay(2000);
                throw new BadRequestException(string.Empty, "Invalid information. Try again.");
            }
        }

        private async Task<User> CheckAsync(string masterPasswordHash, bool premium)
        {
            var user = await _userService.GetUserByPrincipalAsync(User);
            if(user == null)
            {
                throw new UnauthorizedAccessException();
            }

            if(!await _userService.CheckPasswordAsync(user, masterPasswordHash))
            {
                await Task.Delay(2000);
                throw new BadRequestException("MasterPasswordHash", "Invalid password.");
            }

            if(premium && !(await _userService.CanAccessPremium(user)))
            {
                throw new BadRequestException("Premium status is required.");
            }

            return user;
        }

        private async Task ValidateYubiKeyAsync(User user, string name, string value)
        {
            if(string.IsNullOrWhiteSpace(value) || value.Length == 12)
            {
                return;
            }

            if(!await _userManager.VerifyTwoFactorTokenAsync(user,
                CoreHelpers.CustomProviderName(TwoFactorProviderType.YubiKey), value))
            {
                await Task.Delay(2000);
                throw new BadRequestException(name, $"{name} is invalid.");
            }
            else
            {
                await Task.Delay(500);
            }
        }
    }
}
