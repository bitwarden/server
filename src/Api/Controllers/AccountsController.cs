using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Bit.Core.Models.Api;
using Bit.Core.Exceptions;
using Bit.Core.Services;
using Bit.Core.Enums;
using System.Linq;
using Bit.Core.Repositories;
using Bit.Core.Utilities;
using Bit.Core;
using Bit.Core.Models.Business;
using Bit.Api.Utilities;
using Bit.Core.Models.Table;
using System.Collections.Generic;
using Bit.Core.Models.Api.Request.Accounts;
using Bit.Core.Models.Data;

namespace Bit.Api.Controllers
{
    [Route("accounts")]
    [Authorize("Application")]
    public class AccountsController : Controller
    {
        private readonly IUserService _userService;
        private readonly IUserRepository _userRepository;
        private readonly ICipherRepository _cipherRepository;
        private readonly IFolderRepository _folderRepository;
        private readonly IOrganizationUserRepository _organizationUserRepository;
        private readonly IPaymentService _paymentService;
        private readonly GlobalSettings _globalSettings;

        public AccountsController(
            IUserService userService,
            IUserRepository userRepository,
            ICipherRepository cipherRepository,
            IFolderRepository folderRepository,
            IOrganizationUserRepository organizationUserRepository,
            IPaymentService paymentService,
            GlobalSettings globalSettings)
        {
            _userService = userService;
            _userRepository = userRepository;
            _cipherRepository = cipherRepository;
            _folderRepository = folderRepository;
            _organizationUserRepository = organizationUserRepository;
            _paymentService = paymentService;
            _globalSettings = globalSettings;
        }

        [HttpPost("prelogin")]
        [AllowAnonymous]
        public async Task<PreloginResponseModel> PostPrelogin([FromBody]PreloginRequestModel model)
        {
            var kdfInformation = await _userRepository.GetKdfInformationByEmailAsync(model.Email);
            if (kdfInformation == null)
            {
                kdfInformation = new UserKdfInformation
                {
                    Kdf = KdfType.PBKDF2_SHA256,
                    KdfIterations = 100000
                };
            }
            return new PreloginResponseModel(kdfInformation);
        }

        [HttpPost("register")]
        [AllowAnonymous]
        public async Task PostRegister([FromBody]RegisterRequestModel model)
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

        [HttpPost("password-hint")]
        [AllowAnonymous]
        public async Task PostPasswordHint([FromBody]PasswordHintRequestModel model)
        {
            await _userService.SendMasterPasswordHintAsync(model.Email);
        }

        [HttpPost("email-token")]
        public async Task PostEmailToken([FromBody]EmailTokenRequestModel model)
        {
            var user = await _userService.GetUserByPrincipalAsync(User);
            if (user == null)
            {
                throw new UnauthorizedAccessException();
            }

            if (!await _userService.CheckPasswordAsync(user, model.MasterPasswordHash))
            {
                await Task.Delay(2000);
                throw new BadRequestException("MasterPasswordHash", "Invalid password.");
            }

            await _userService.InitiateEmailChangeAsync(user, model.NewEmail);
        }

        [HttpPost("email")]
        public async Task PostEmail([FromBody]EmailRequestModel model)
        {
            var user = await _userService.GetUserByPrincipalAsync(User);
            if (user == null)
            {
                throw new UnauthorizedAccessException();
            }

            var result = await _userService.ChangeEmailAsync(user, model.MasterPasswordHash, model.NewEmail,
                model.NewMasterPasswordHash, model.Token, model.Key);
            if (result.Succeeded)
            {
                return;
            }

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }

            await Task.Delay(2000);
            throw new BadRequestException(ModelState);
        }

        [HttpPost("verify-email")]
        public async Task PostVerifyEmail()
        {
            var user = await _userService.GetUserByPrincipalAsync(User);
            if (user == null)
            {
                throw new UnauthorizedAccessException();
            }

            await _userService.SendEmailVerificationAsync(user);
        }

        [HttpPost("verify-email-token")]
        [AllowAnonymous]
        public async Task PostVerifyEmailToken([FromBody]VerifyEmailRequestModel model)
        {
            var user = await _userService.GetUserByIdAsync(new Guid(model.UserId));
            if (user == null)
            {
                throw new UnauthorizedAccessException();
            }
            var result = await _userService.ConfirmEmailAsync(user, model.Token);
            if (result.Succeeded)
            {
                return;
            }

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }

            await Task.Delay(2000);
            throw new BadRequestException(ModelState);
        }

        [HttpPost("password")]
        public async Task PostPassword([FromBody]PasswordRequestModel model)
        {
            var user = await _userService.GetUserByPrincipalAsync(User);
            if (user == null)
            {
                throw new UnauthorizedAccessException();
            }

            var result = await _userService.ChangePasswordAsync(user, model.MasterPasswordHash,
                model.NewMasterPasswordHash, model.Key);
            if (result.Succeeded)
            {
                return;
            }

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }

            await Task.Delay(2000);
            throw new BadRequestException(ModelState);
        }
        
        [HttpPost("set-password")]
        public async Task SetPasswordAsync([FromBody]SetPasswordRequestModel model)
        {
            var user = await _userService.GetUserByPrincipalAsync(User);
            if (user == null)
            {
                throw new UnauthorizedAccessException();
            }

            var result = await _userService.SetPasswordAsync(user, model.NewMasterPasswordHash, model.Key);
            if (result.Succeeded)
            {
                return;
            }

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }

            throw new BadRequestException(ModelState);
        }

        [HttpPost("verify-password")]
        public async Task PostVerifyPassword([FromBody]VerifyPasswordRequestModel model)
        {
            var user = await _userService.GetUserByPrincipalAsync(User);
            if (user == null)
            {
                throw new UnauthorizedAccessException();
            }

            if (await _userService.CheckPasswordAsync(user, model.MasterPasswordHash))
            {
                return;
            }

            ModelState.AddModelError(nameof(model.MasterPasswordHash), "Invalid password.");
            await Task.Delay(2000);
            throw new BadRequestException(ModelState);
        }

        [HttpPost("kdf")]
        public async Task PostKdf([FromBody]KdfRequestModel model)
        {
            var user = await _userService.GetUserByPrincipalAsync(User);
            if (user == null)
            {
                throw new UnauthorizedAccessException();
            }

            var result = await _userService.ChangeKdfAsync(user, model.MasterPasswordHash,
                model.NewMasterPasswordHash, model.Key, model.Kdf.Value, model.KdfIterations.Value);
            if (result.Succeeded)
            {
                return;
            }

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }

            await Task.Delay(2000);
            throw new BadRequestException(ModelState);
        }

        [HttpPost("key")]
        public async Task PostKey([FromBody]UpdateKeyRequestModel model)
        {
            var user = await _userService.GetUserByPrincipalAsync(User);
            if (user == null)
            {
                throw new UnauthorizedAccessException();
            }

            var existingCiphers = await _cipherRepository.GetManyByUserIdAsync(user.Id);
            var ciphersDict = model.Ciphers?.ToDictionary(c => c.Id.Value);
            var ciphers = new List<Cipher>();
            if (existingCiphers.Any() && ciphersDict != null)
            {
                foreach (var cipher in existingCiphers.Where(c => ciphersDict.ContainsKey(c.Id)))
                {
                    ciphers.Add(ciphersDict[cipher.Id].ToCipher(cipher));
                }
            }

            var existingFolders = await _folderRepository.GetManyByUserIdAsync(user.Id);
            var foldersDict = model.Folders?.ToDictionary(f => f.Id);
            var folders = new List<Folder>();
            if (existingFolders.Any() && foldersDict != null)
            {
                foreach (var folder in existingFolders.Where(f => foldersDict.ContainsKey(f.Id)))
                {
                    folders.Add(foldersDict[folder.Id].ToFolder(folder));
                }
            }

            var result = await _userService.UpdateKeyAsync(
                user,
                model.MasterPasswordHash,
                model.Key,
                model.PrivateKey,
                ciphers,
                folders);

            if (result.Succeeded)
            {
                return;
            }

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }

            await Task.Delay(2000);
            throw new BadRequestException(ModelState);
        }

        [HttpPost("security-stamp")]
        public async Task PostSecurityStamp([FromBody]SecurityStampRequestModel model)
        {
            var user = await _userService.GetUserByPrincipalAsync(User);
            if (user == null)
            {
                throw new UnauthorizedAccessException();
            }

            var result = await _userService.RefreshSecurityStampAsync(user, model.MasterPasswordHash);
            if (result.Succeeded)
            {
                return;
            }

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }

            await Task.Delay(2000);
            throw new BadRequestException(ModelState);
        }

        [HttpGet("profile")]
        public async Task<ProfileResponseModel> GetProfile()
        {
            var user = await _userService.GetUserByPrincipalAsync(User);
            if (user == null)
            {
                throw new UnauthorizedAccessException();
            }

            var organizationUserDetails = await _organizationUserRepository.GetManyDetailsByUserAsync(user.Id,
                OrganizationUserStatusType.Confirmed);
            var response = new ProfileResponseModel(user, organizationUserDetails,
                await _userService.TwoFactorIsEnabledAsync(user));
            return response;
        }

        [HttpGet("organizations")]
        public async Task<ListResponseModel<ProfileOrganizationResponseModel>> GetOrganizations()
        {
            var userId = _userService.GetProperUserId(User);
            var organizationUserDetails = await _organizationUserRepository.GetManyDetailsByUserAsync(userId.Value,
                OrganizationUserStatusType.Confirmed);
            var responseData = organizationUserDetails.Select(o => new ProfileOrganizationResponseModel(o));
            return new ListResponseModel<ProfileOrganizationResponseModel>(responseData);
        }

        [HttpPut("profile")]
        [HttpPost("profile")]
        public async Task<ProfileResponseModel> PutProfile([FromBody]UpdateProfileRequestModel model)
        {
            var user = await _userService.GetUserByPrincipalAsync(User);
            if (user == null)
            {
                throw new UnauthorizedAccessException();
            }

            await _userService.SaveUserAsync(model.ToUser(user));
            var response = new ProfileResponseModel(user, null, await _userService.TwoFactorIsEnabledAsync(user));
            return response;
        }

        [HttpGet("revision-date")]
        public async Task<long?> GetAccountRevisionDate()
        {
            var userId = _userService.GetProperUserId(User);
            long? revisionDate = null;
            if (userId.HasValue)
            {
                var date = await _userService.GetAccountRevisionDateByIdAsync(userId.Value);
                revisionDate = CoreHelpers.ToEpocMilliseconds(date);
            }

            return revisionDate;
        }

        [HttpPost("keys")]
        public async Task<KeysResponseModel> PostKeys([FromBody]KeysRequestModel model)
        {
            var user = await _userService.GetUserByPrincipalAsync(User);
            if (user == null)
            {
                throw new UnauthorizedAccessException();
            }

            await _userService.SaveUserAsync(model.ToUser(user));
            return new KeysResponseModel(user);
        }

        [HttpGet("keys")]
        public async Task<KeysResponseModel> GetKeys()
        {
            var user = await _userService.GetUserByPrincipalAsync(User);
            if (user == null)
            {
                throw new UnauthorizedAccessException();
            }

            return new KeysResponseModel(user);
        }

        [HttpDelete]
        [HttpPost("delete")]
        public async Task Delete([FromBody]DeleteAccountRequestModel model)
        {
            var user = await _userService.GetUserByPrincipalAsync(User);
            if (user == null)
            {
                throw new UnauthorizedAccessException();
            }

            if (!await _userService.CheckPasswordAsync(user, model.MasterPasswordHash))
            {
                ModelState.AddModelError("MasterPasswordHash", "Invalid password.");
                await Task.Delay(2000);
            }
            else
            {
                var result = await _userService.DeleteAsync(user);
                if (result.Succeeded)
                {
                    return;
                }

                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }
            }

            throw new BadRequestException(ModelState);
        }

        [AllowAnonymous]
        [HttpPost("delete-recover")]
        public async Task PostDeleteRecover([FromBody]DeleteRecoverRequestModel model)
        {
            await _userService.SendDeleteConfirmationAsync(model.Email);
        }

        [HttpPost("delete-recover-token")]
        [AllowAnonymous]
        public async Task PostDeleteRecoverToken([FromBody]VerifyDeleteRecoverRequestModel model)
        {
            var user = await _userService.GetUserByIdAsync(new Guid(model.UserId));
            if (user == null)
            {
                throw new UnauthorizedAccessException();
            }

            var result = await _userService.DeleteAsync(user, model.Token);
            if (result.Succeeded)
            {
                return;
            }

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }

            await Task.Delay(2000);
            throw new BadRequestException(ModelState);
        }

        [HttpPost("iap-check")]
        public async Task PostIapCheck([FromBody]IapCheckRequestModel model)
        {
            var user = await _userService.GetUserByPrincipalAsync(User);
            if (user == null)
            {
                throw new UnauthorizedAccessException();
            }
            await _userService.IapCheckAsync(user, model.PaymentMethodType.Value);
        }

        [HttpPost("premium")]
        public async Task<PaymentResponseModel> PostPremium(PremiumRequestModel model)
        {
            var user = await _userService.GetUserByPrincipalAsync(User);
            if (user == null)
            {
                throw new UnauthorizedAccessException();
            }

            var valid = model.Validate(_globalSettings);
            UserLicense license = null;
            if (valid && _globalSettings.SelfHosted)
            {
                license = await ApiHelpers.ReadJsonFileFromBody<UserLicense>(HttpContext, model.License);
            }

            if (!valid && !_globalSettings.SelfHosted && string.IsNullOrWhiteSpace(model.Country))
            {
                throw new BadRequestException("Country is required.");
            }

            if (!valid || (_globalSettings.SelfHosted && license == null))
            {
                throw new BadRequestException("Invalid license.");
            }

            var result = await _userService.SignUpPremiumAsync(user, model.PaymentToken,
                model.PaymentMethodType.Value, model.AdditionalStorageGb.GetValueOrDefault(0), license,
                new TaxInfo
                {
                    BillingAddressCountry = model.Country,
                    BillingAddressPostalCode = model.PostalCode,
                });
            var profile = new ProfileResponseModel(user, null, await _userService.TwoFactorIsEnabledAsync(user));
            return new PaymentResponseModel
            {
                UserProfile = profile,
                PaymentIntentClientSecret = result.Item2,
                Success = result.Item1
            };
        }

        [HttpGet("billing")]
        [SelfHosted(NotSelfHostedOnly = true)]
        public async Task<BillingResponseModel> GetBilling()
        {
            var user = await _userService.GetUserByPrincipalAsync(User);
            if (user == null)
            {
                throw new UnauthorizedAccessException();
            }

            var billingInfo = await _paymentService.GetBillingAsync(user);
            return new BillingResponseModel(billingInfo);
        }

        [HttpGet("subscription")]
        public async Task<SubscriptionResponseModel> GetSubscription()
        {
            var user = await _userService.GetUserByPrincipalAsync(User);
            if (user == null)
            {
                throw new UnauthorizedAccessException();
            }

            if (!_globalSettings.SelfHosted && user.Gateway != null)
            {
                var subscriptionInfo = await _paymentService.GetSubscriptionAsync(user);
                var license = await _userService.GenerateLicenseAsync(user, subscriptionInfo);
                return new SubscriptionResponseModel(user, subscriptionInfo, license);
            }
            else if (!_globalSettings.SelfHosted)
            {
                var license = await _userService.GenerateLicenseAsync(user);
                return new SubscriptionResponseModel(user, license);
            }
            else
            {
                return new SubscriptionResponseModel(user);
            }
        }

        [HttpPost("payment")]
        [SelfHosted(NotSelfHostedOnly = true)]
        public async Task PostPayment([FromBody]PaymentRequestModel model)
        {
            var user = await _userService.GetUserByPrincipalAsync(User);
            if (user == null)
            {
                throw new UnauthorizedAccessException();
            }

            await _userService.ReplacePaymentMethodAsync(user, model.PaymentToken, model.PaymentMethodType.Value,
                new TaxInfo
                {
                    BillingAddressCountry = model.Country,
                    BillingAddressPostalCode = model.PostalCode,
                });
        }

        [HttpPost("storage")]
        [SelfHosted(NotSelfHostedOnly = true)]
        public async Task<PaymentResponseModel> PostStorage([FromBody]StorageRequestModel model)
        {
            var user = await _userService.GetUserByPrincipalAsync(User);
            if (user == null)
            {
                throw new UnauthorizedAccessException();
            }

            var result = await _userService.AdjustStorageAsync(user, model.StorageGbAdjustment.Value);
            return new PaymentResponseModel
            {
                Success = true,
                PaymentIntentClientSecret = result
            };
        }

        [HttpPost("license")]
        [SelfHosted(SelfHostedOnly = true)]
        public async Task PostLicense(LicenseRequestModel model)
        {
            var user = await _userService.GetUserByPrincipalAsync(User);
            if (user == null)
            {
                throw new UnauthorizedAccessException();
            }

            var license = await ApiHelpers.ReadJsonFileFromBody<UserLicense>(HttpContext, model.License);
            if (license == null)
            {
                throw new BadRequestException("Invalid license");
            }

            await _userService.UpdateLicenseAsync(user, license);
        }

        [HttpPost("cancel-premium")]
        [SelfHosted(NotSelfHostedOnly = true)]
        public async Task PostCancel()
        {
            var user = await _userService.GetUserByPrincipalAsync(User);
            if (user == null)
            {
                throw new UnauthorizedAccessException();
            }

            await _userService.CancelPremiumAsync(user);
        }

        [HttpPost("reinstate-premium")]
        [SelfHosted(NotSelfHostedOnly = true)]
        public async Task PostReinstate()
        {
            var user = await _userService.GetUserByPrincipalAsync(User);
            if (user == null)
            {
                throw new UnauthorizedAccessException();
            }

            await _userService.ReinstatePremiumAsync(user);
        }

        [HttpGet("enterprise-portal-signin-token")]
        [Authorize("Web")]
        public async Task<string> GetEnterprisePortalSignInToken()
        {
            var user = await _userService.GetUserByPrincipalAsync(User);
            if (user == null)
            {
                throw new UnauthorizedAccessException();
            }

            var token = await _userService.GenerateEnterprisePortalSignInTokenAsync(user);
            if (token == null)
            {
                throw new BadRequestException("Cannot generate sign in token.");
            }

            return token;
        }

        [HttpGet("tax")]
        [SelfHosted(NotSelfHostedOnly = true)]
        public async Task<TaxInfoResponseModel> GetTaxInfo()
        {
            var user = await _userService.GetUserByPrincipalAsync(User);
            if (user == null)
            {
                throw new UnauthorizedAccessException();
            }

            var taxInfo = await _paymentService.GetTaxInfoAsync(user);
            return new TaxInfoResponseModel(taxInfo);
        }

        [HttpPut("tax")]
        [SelfHosted(NotSelfHostedOnly = true)]
        public async Task PutTaxInfo([FromBody]TaxInfoUpdateRequestModel model)
        {
            var user = await _userService.GetUserByPrincipalAsync(User);
            if (user == null)
            {
                throw new UnauthorizedAccessException();
            }

            var taxInfo = new TaxInfo
            {
                BillingAddressPostalCode = model.PostalCode,
                BillingAddressCountry = model.Country,
            };
            await _paymentService.SaveTaxInfoAsync(user, taxInfo);
        }
    }
}
