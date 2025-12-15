using Bit.Api.AdminConsole.Models.Request.Organizations;
using Bit.Api.Auth.Models.Request;
using Bit.Api.Auth.Models.Request.WebAuthn;
using Bit.Api.KeyManagement.Models.Requests;
using Bit.Api.KeyManagement.Models.Responses;
using Bit.Api.KeyManagement.Validators;
using Bit.Api.Tools.Models.Request;
using Bit.Api.Vault.Models.Request;
using Bit.Core;
using Bit.Core.Auth.Entities;
using Bit.Core.Auth.Models.Api.Request;
using Bit.Core.Auth.Models.Data;
using Bit.Core.Entities;
using Bit.Core.Exceptions;
using Bit.Core.KeyManagement.Commands.Interfaces;
using Bit.Core.KeyManagement.Models.Data;
using Bit.Core.KeyManagement.Queries.Interfaces;
using Bit.Core.KeyManagement.UserKey;
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
    private readonly IFeatureService _featureService;
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

    public AccountsKeyManagementController(IUserService userService,
        IFeatureService featureService,
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
        IRotationValidator<IEnumerable<WebAuthnLoginRotateKeyRequestModel>, IEnumerable<WebAuthnLoginRotateKeyData>> webAuthnKeyValidator,
        IRotationValidator<IEnumerable<OtherDeviceKeysUpdateRequestModel>, IEnumerable<Device>> deviceValidator)
    {
        _userService = userService;
        _featureService = featureService;
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
    }

    [HttpPost("key-management/regenerate-keys")]
    public async Task RegenerateKeysAsync([FromBody] KeyRegenerationRequestModel request)
    {
        if (!_featureService.IsEnabled(FeatureFlagKeys.PrivateKeyRegeneration) && !_featureService.IsEnabled(FeatureFlagKeys.DataRecoveryTool))
        {
            throw new NotFoundException();
        }

        var user = await _userService.GetUserByPrincipalAsync(User) ?? throw new UnauthorizedAccessException();
        var usersOrganizationAccounts = await _organizationUserRepository.GetManyByUserAsync(user.Id);
        var designatedEmergencyAccess = await _emergencyAccessRepository.GetManyDetailsByGranteeIdAsync(user.Id);
        await _regenerateUserAsymmetricKeysCommand.RegenerateKeysAsync(request.ToUserAsymmetricKeys(user.Id),
            usersOrganizationAccounts, designatedEmergencyAccess);
    }


    [HttpPost("key-management/rotate-user-account-keys")]
    public async Task RotateUserAccountKeysAsync([FromBody] RotateUserAccountKeysAndDataRequestModel model)
    {
        var user = await _userService.GetUserByPrincipalAsync(User);
        if (user == null)
        {
            throw new UnauthorizedAccessException();
        }

        var dataModel = new RotateUserAccountKeysData
        {
            OldMasterKeyAuthenticationHash = model.OldMasterKeyAuthenticationHash,

            AccountKeys = model.AccountKeys.ToAccountKeysData(),

            MasterPasswordUnlockData = model.AccountUnlockData.MasterPasswordUnlockData.ToUnlockData(),
            EmergencyAccesses = await _emergencyAccessValidator.ValidateAsync(user, model.AccountUnlockData.EmergencyAccessUnlockData),
            OrganizationUsers = await _organizationUserValidator.ValidateAsync(user, model.AccountUnlockData.OrganizationAccountRecoveryUnlockData),
            WebAuthnKeys = await _webauthnKeyValidator.ValidateAsync(user, model.AccountUnlockData.PasskeyUnlockData),
            DeviceKeys = await _deviceValidator.ValidateAsync(user, model.AccountUnlockData.DeviceKeyUnlockData),

            Ciphers = await _cipherValidator.ValidateAsync(user, model.AccountData.Ciphers),
            Folders = await _folderValidator.ValidateAsync(user, model.AccountData.Folders),
            Sends = await _sendValidator.ValidateAsync(user, model.AccountData.Sends),
        };

        var result = await _rotateUserAccountKeysCommand.RotateUserAccountKeysAsync(user, dataModel);
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

    [HttpPost("set-key-connector-key")]
    public async Task PostSetKeyConnectorKeyAsync([FromBody] SetKeyConnectorKeyRequestModel model)
    {
        var user = await _userService.GetUserByPrincipalAsync(User);
        if (user == null)
        {
            throw new UnauthorizedAccessException();
        }

        var result = await _userService.SetKeyConnectorKeyAsync(model.ToUser(user), model.Key, model.OrgIdentifier);
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

    [HttpPost("convert-to-key-connector")]
    public async Task PostConvertToKeyConnectorAsync()
    {
        var user = await _userService.GetUserByPrincipalAsync(User);
        if (user == null)
        {
            throw new UnauthorizedAccessException();
        }

        var result = await _userService.ConvertToKeyConnectorAsync(user);
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
}
