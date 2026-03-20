using Bit.Api.AdminConsole.Models.Request.Organizations;
using Bit.Api.Auth.Models.Request;
using Bit.Api.Auth.Models.Request.WebAuthn;
using Bit.Api.KeyManagement.Enums;
using Bit.Api.KeyManagement.Models.Requests;
using Bit.Api.KeyManagement.Models.Responses;
using Bit.Api.KeyManagement.Validators;
using Bit.Api.Tools.Models.Request;
using Bit.Api.Vault.Models.Request;
using Bit.Core.Auth.Entities;
using Bit.Core.Auth.Models.Api.Request;
using Bit.Core.Auth.Models.Data;
using Bit.Core.Entities;
using Bit.Core.Exceptions;
using Bit.Core.KeyManagement.Commands.Interfaces;
using Bit.Core.KeyManagement.Queries.Interfaces;
using Bit.Core.KeyManagement.UserKey;
using Bit.Core.KeyManagement.UserKey.Models.Data;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Tools.Entities;
using Bit.Core.Vault.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Api.KeyManagement.Controllers;

[Route("accounts")]
[Authorize("Application")]
public class AccountsKeyManagementController : Controller
{
    private readonly IEmergencyAccessRepository _emergencyAccessRepository;
    private readonly IOrganizationUserRepository _organizationUserRepository;
    private readonly IRegenerateUserAsymmetricKeysCommand _regenerateUserAsymmetricKeysCommand;
    private readonly IUserService _userService;
    private readonly IRotateUserAccountKeysCommand _rotateUserAccountKeysCommand;
    private readonly IRotationValidator<IEnumerable<CipherWithIdRequestModel>, IEnumerable<Cipher>> _cipherValidator;
    private readonly IRotationValidator<IEnumerable<FolderWithIdRequestModel>, IEnumerable<Folder>> _folderValidator;
    private readonly IRotationValidator<IEnumerable<SendWithIdRequestModel>, IReadOnlyList<Send>> _sendValidator;
    private readonly IRotationValidator<IEnumerable<EmergencyAccessWithIdRequestModel>, IEnumerable<EmergencyAccess>>
        _emergencyAccessValidator;
    private readonly IRotationValidator<IEnumerable<ResetPasswordWithOrgIdRequestModel>,
            IReadOnlyList<OrganizationUser>>
        _organizationUserValidator;
    private readonly IRotationValidator<IEnumerable<WebAuthnLoginRotateKeyRequestModel>, IEnumerable<WebAuthnLoginRotateKeyData>>
        _webauthnKeyValidator;
    private readonly IRotationValidator<IEnumerable<OtherDeviceKeysUpdateRequestModel>, IEnumerable<Device>> _deviceValidator;
    private readonly IKeyConnectorConfirmationDetailsQuery _keyConnectorConfirmationDetailsQuery;
    private readonly ISetKeyConnectorKeyCommand _setKeyConnectorKeyCommand;

    public AccountsKeyManagementController(IUserService userService,
        IOrganizationUserRepository organizationUserRepository,
        IEmergencyAccessRepository emergencyAccessRepository,
        IKeyConnectorConfirmationDetailsQuery keyConnectorConfirmationDetailsQuery,
        IRegenerateUserAsymmetricKeysCommand regenerateUserAsymmetricKeysCommand,
        IRotateUserAccountKeysCommand rotateUserKeyCommandV2,
        IRotationValidator<IEnumerable<CipherWithIdRequestModel>, IEnumerable<Cipher>> cipherValidator,
        IRotationValidator<IEnumerable<FolderWithIdRequestModel>, IEnumerable<Folder>> folderValidator,
        IRotationValidator<IEnumerable<SendWithIdRequestModel>, IReadOnlyList<Send>> sendValidator,
        IRotationValidator<IEnumerable<EmergencyAccessWithIdRequestModel>, IEnumerable<EmergencyAccess>>
            emergencyAccessValidator,
        IRotationValidator<IEnumerable<ResetPasswordWithOrgIdRequestModel>, IReadOnlyList<OrganizationUser>>
            organizationUserValidator,
        IRotationValidator<IEnumerable<WebAuthnLoginRotateKeyRequestModel>, IEnumerable<WebAuthnLoginRotateKeyData>>
            webAuthnKeyValidator,
        IRotationValidator<IEnumerable<OtherDeviceKeysUpdateRequestModel>, IEnumerable<Device>> deviceValidator,
        ISetKeyConnectorKeyCommand setKeyConnectorKeyCommand)
    {
        _userService = userService;
        _regenerateUserAsymmetricKeysCommand = regenerateUserAsymmetricKeysCommand;
        _organizationUserRepository = organizationUserRepository;
        _emergencyAccessRepository = emergencyAccessRepository;
        _rotateUserAccountKeysCommand = rotateUserKeyCommandV2;
        _cipherValidator = cipherValidator;
        _folderValidator = folderValidator;
        _sendValidator = sendValidator;
        _emergencyAccessValidator = emergencyAccessValidator;
        _organizationUserValidator = organizationUserValidator;
        _webauthnKeyValidator = webAuthnKeyValidator;
        _deviceValidator = deviceValidator;
        _keyConnectorConfirmationDetailsQuery = keyConnectorConfirmationDetailsQuery;
        _setKeyConnectorKeyCommand = setKeyConnectorKeyCommand;
    }

    [HttpPost("key-management/regenerate-keys")]
    public async Task RegenerateKeysAsync([FromBody] KeyRegenerationRequestModel request)
    {
        var user = await _userService.GetUserByPrincipalAsync(User) ?? throw new UnauthorizedAccessException();
        var usersOrganizationAccounts = await _organizationUserRepository.GetManyByUserAsync(user.Id);
        var designatedEmergencyAccess = await _emergencyAccessRepository.GetManyDetailsByGranteeIdAsync(user.Id);
        await _regenerateUserAsymmetricKeysCommand.RegenerateKeysAsync(request.ToUserAsymmetricKeys(user.Id),
            usersOrganizationAccounts, designatedEmergencyAccess);
    }


    [HttpPost("key-management/rotate-user-account-keys")]
    public async Task PasswordChangeAndRotateUserAccountKeysAsync([FromBody] RotateUserAccountKeysAndDataRequestModel model)
    {
        var user = await _userService.GetUserByPrincipalAsync(User);
        if (user == null)
        {
            throw new UnauthorizedAccessException();
        }

        var dataModel = new PasswordChangeAndRotateUserAccountKeysData
        {
            OldMasterKeyAuthenticationHash = model.OldMasterKeyAuthenticationHash,
            MasterPasswordHint = model.AccountUnlockData.MasterPasswordUnlockData.MasterPasswordHint,
            MasterPasswordAuthenticationData = model.AccountUnlockData.MasterPasswordUnlockData.ToAuthenticationData(),
            MasterPasswordUnlockData = model.AccountUnlockData.MasterPasswordUnlockData.ToMasterPasswordUnlockData(),
            BaseData = new BaseRotateUserAccountKeysData
            {
                AccountKeys = model.AccountKeys.ToAccountKeysData(),
                EmergencyAccesses =
                    await _emergencyAccessValidator.ValidateAsync(user,
                        model.AccountUnlockData.EmergencyAccessUnlockData),
                OrganizationUsers =
                    await _organizationUserValidator.ValidateAsync(user,
                        model.AccountUnlockData.OrganizationAccountRecoveryUnlockData),
                WebAuthnKeys =
                    await _webauthnKeyValidator.ValidateAsync(user, model.AccountUnlockData.PasskeyUnlockData),
                DeviceKeys = await _deviceValidator.ValidateAsync(user, model.AccountUnlockData.DeviceKeyUnlockData),
                V2UpgradeToken = model.AccountUnlockData.V2UpgradeToken?.ToData(),
                Ciphers = await _cipherValidator.ValidateAsync(user, model.AccountData.Ciphers),
                Folders = await _folderValidator.ValidateAsync(user, model.AccountData.Folders),
                Sends = await _sendValidator.ValidateAsync(user, model.AccountData.Sends)
            }
        };

        var result = await _rotateUserAccountKeysCommand.PasswordChangeAndRotateUserAccountKeysAsync(user, dataModel);
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

    [HttpPost("key-management/rotate-user-keys")]
    public async Task RotateUserKeysAsync([FromBody] RotateUserKeysRequestModel request)
    {
        var user = await _userService.GetUserByPrincipalAsync(User);
        if (user == null)
        {
            throw new UnauthorizedAccessException();
        }

        switch (request.UnlockMethodData.UnlockMethod)
        {
            case UnlockMethod.MasterPassword:
                var dataModel = new MasterPasswordRotateUserAccountKeysData
                {
                    MasterPasswordUnlockData = request.UnlockMethodData.MasterPasswordUnlockData!.ToData(),
                    BaseData = await ToBaseDataModelAsync(request, user),
                };
                await _rotateUserAccountKeysCommand.MasterPasswordRotateUserAccountKeysAsync(user, dataModel);
                break;
            case UnlockMethod.Tde:
                throw new BadRequestException("TDE not implemented");
            case UnlockMethod.KeyConnector:
                throw new BadRequestException("Key connector not implemented");
            default:
                throw new ArgumentOutOfRangeException(nameof(request.UnlockMethodData.UnlockMethod), "Unrecognized unlock method");
        }
    }

    [HttpPost("set-key-connector-key")]
    public async Task PostSetKeyConnectorKeyAsync([FromBody] SetKeyConnectorKeyRequestModel model)
    {
        var user = await _userService.GetUserByPrincipalAsync(User);
        if (user == null)
        {
            throw new UnauthorizedAccessException();
        }

        if (model.IsV2Request())
        {
            // V2 account registration
            await _setKeyConnectorKeyCommand.SetKeyConnectorKeyForUserAsync(user, model.ToKeyConnectorKeysData());
        }
        else
        {
            // V1 account registration
            // TODO removed with https://bitwarden.atlassian.net/browse/PM-27328
            var result = await _userService.SetKeyConnectorKeyAsync(model.ToUser(user), model.Key!, model.OrgIdentifier);
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
    }

    [HttpPost("convert-to-key-connector")]
    public async Task PostConvertToKeyConnectorAsync()
    {
        var user = await _userService.GetUserByPrincipalAsync(User);
        if (user == null)
        {
            throw new UnauthorizedAccessException();
        }

        var result = await _userService.ConvertToKeyConnectorAsync(user, null);
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

    [HttpPost("key-connector/enroll")]
    public async Task PostEnrollToKeyConnectorAsync([FromBody] KeyConnectorEnrollmentRequestModel model)
    {
        var user = await _userService.GetUserByPrincipalAsync(User);
        if (user == null)
        {
            throw new UnauthorizedAccessException();
        }

        var result = await _userService.ConvertToKeyConnectorAsync(user, model.KeyConnectorKeyWrappedUserKey);
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

    [HttpGet("key-connector/confirmation-details/{orgSsoIdentifier}")]
    public async Task<KeyConnectorConfirmationDetailsResponseModel> GetKeyConnectorConfirmationDetailsAsync(string orgSsoIdentifier)
    {
        var user = await _userService.GetUserByPrincipalAsync(User);
        if (user == null)
        {
            throw new UnauthorizedAccessException();
        }

        var details = await _keyConnectorConfirmationDetailsQuery.Run(orgSsoIdentifier, user.Id);
        return new KeyConnectorConfirmationDetailsResponseModel(details);
    }

    private async Task<BaseRotateUserAccountKeysData> ToBaseDataModelAsync(RotateUserKeysRequestModel request, User user)
    {
        return new BaseRotateUserAccountKeysData
        {
            AccountKeys = request.WrappedAccountCryptographicState.ToAccountKeysData(),
            EmergencyAccesses =
                await _emergencyAccessValidator.ValidateAsync(user, request.UnlockData.EmergencyAccessUnlockData),
            OrganizationUsers =
                await _organizationUserValidator.ValidateAsync(user,
                    request.UnlockData.OrganizationAccountRecoveryUnlockData),
            WebAuthnKeys = await _webauthnKeyValidator.ValidateAsync(user, request.UnlockData.PasskeyUnlockData),
            DeviceKeys = await _deviceValidator.ValidateAsync(user, request.UnlockData.DeviceKeyUnlockData),
            V2UpgradeToken = request.UnlockData.V2UpgradeToken?.ToData(),

            Ciphers = await _cipherValidator.ValidateAsync(user, request.AccountData.Ciphers),
            Folders = await _folderValidator.ValidateAsync(user, request.AccountData.Folders),
            Sends = await _sendValidator.ValidateAsync(user, request.AccountData.Sends),
        };
    }
}
