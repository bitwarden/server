using Bit.Api.Auth.Models.Request.SsoJitProvisionFinish;
using Bit.Core;
using Bit.Core.Auth.Identity;
using Bit.Core.Auth.UserFeatures.UserMasterPassword.Interfaces;
using Bit.Core.Entities;
using Bit.Core.Exceptions;
using Bit.Core.KeyManagement.Commands.Interfaces;
using Bit.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Api.Auth.Controllers;

/// <summary>
/// Finishes onboarding for SSO JIT provisioned users by member decryption type. Each request's required
/// <c>AcceptOrganizationInvite</c> flag controls whether the user's pending organization invite is accepted. Open-invite clients send <c>false</c> so the member stays Invited for later Admin Console checks.
/// </summary>
[Route("sso-jit-provision-finish")]
[Authorize(Policies.Application)]
public class SsoJitProvisionFinishController(
    IUserService userService,
    Bitwarden.Server.Sdk.Features.IFeatureService featureService,
    IFinishSsoJitProvisionMasterPasswordCommand finishSsoJitProvisionMasterPasswordCommand,
    ISetInitialMasterPasswordCommandV1 setInitialMasterPasswordCommandV1,
    ISetKeyConnectorKeyCommand setKeyConnectorKeyCommand) : Controller
{
    private readonly IUserService _userService = userService;
    private readonly Bitwarden.Server.Sdk.Features.IFeatureService _featureService = featureService;
    private readonly IFinishSsoJitProvisionMasterPasswordCommand _finishSsoJitProvisionMasterPasswordCommand =
        finishSsoJitProvisionMasterPasswordCommand;
    private readonly ISetInitialMasterPasswordCommandV1 _setInitialMasterPasswordCommandV1 = setInitialMasterPasswordCommandV1;
    private readonly ISetKeyConnectorKeyCommand _setKeyConnectorKeyCommand = setKeyConnectorKeyCommand;

    /// <summary>
    /// Sets the initial master password and account keys for an SSO JIT provisioned user in an organization
    /// that uses master password decryption. Accepts the pending organization invite only when
    /// <see cref="SsoJitProvisionFinishPasswordRequestModel.AcceptOrganizationInvite"/> is <c>true</c>.
    /// </summary>
    /// <remarks>
    /// Accepts MPAD + MPUD with exactly one keypair: V2 encryption (AccountKeys, V2 MP JIT flag on)
    /// or V1 encryption (legacy Keys).
    /// </remarks>
    [HttpPost("password")]
    public async Task FinishJitProvisionForPasswordAsync([FromBody] SsoJitProvisionFinishPasswordRequestModel model)
    {
        var user = await _userService.GetUserByPrincipalAsync(User) ?? throw new UnauthorizedAccessException();

        // TODO: eventually move this into the FinishSsoJitProvisionMasterPasswordCommand
        if (user.PublicKey != null || user.PrivateKey != null)
        {
            throw new BadRequestException("User account keys are already initialized.");
        }

        // Require AccountKeys so V1 requests (legacy Keys) still route to V1 when the flag is on.
        if (model.AccountKeys != null &&
            _featureService.IsEnabled(FeatureFlagKeys.EnableAccountEncryptionV2JitPasswordRegistration))
        {
            await _finishSsoJitProvisionMasterPasswordCommand.FinishProvisionAsync(
                user, model.ToData(), model.AcceptOrganizationInvite);
            return;
        }

        await FinishJitProvisionForPasswordV1Async(user, model);
    }

    /// <summary>
    /// Handles finishing JIT provisioning for V1 users in the following scenario:
    /// 1. MP JIT user where request contains MPAD + MPUD + legacy Keys (not AccountKeys)
    ///
    /// Mirrors <c>AccountsController.SetInitialPasswordV1Async</c> with the option of not accepting the
    /// organization invite. Unlike that method, this endpoint accepts no legacy-shape requests.
    /// </summary>
    /// <remarks>
    /// TODO: In order to remove this method, the scenario above needs to become unused. This means removal can
    /// only happen when the EnableAccountEncryptionV2JitPasswordRegistration feature flag has been unwound in
    /// https://bitwarden.atlassian.net/browse/PM-27327 and the client-side portion of the flag removal has aged
    /// out according to the Bitwarden release support policy.
    /// </remarks>
    private async Task FinishJitProvisionForPasswordV1Async(
        User user,
        SsoJitProvisionFinishPasswordRequestModel model)
    {
        // V1 cannot consume AccountKeys (the new key shape). Fail instead of silently dropping the keypair.
        if (model.AccountKeys != null)
        {
            throw new BadRequestException(
                "Request includes V2 AccountKeys but V2 encryption is not enabled.");
        }

        try
        {
            // Stage 1 (PM-27044) compatibility: see AccountsController.SetInitialPasswordV1Async.
            model.MasterPasswordUnlock.ToData().ValidateSaltUnchangedForUser(user);
            model.MasterPasswordAuthentication.ToData().ValidateSaltUnchangedForUser(user);

            user = model.ToUser(user);
        }
        catch (Exception e)
        {
            ModelState.AddModelError(string.Empty, e.Message);
            throw new BadRequestException(ModelState);
        }

        var result = await _setInitialMasterPasswordCommandV1.SetInitialMasterPasswordAsync(
            user,
            model.MasterPasswordAuthentication.MasterPasswordAuthenticationHash,
            model.MasterPasswordUnlock.MasterKeyWrappedUserKey,
            model.OrgIdentifier,
            model.AcceptOrganizationInvite);

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

    /// <summary>
    /// Sets the Key Connector wrapped user key and account keys for an SSO JIT provisioned user in an organization
    /// that uses Key Connector decryption. Accepts the organization membership only when
    /// <see cref="SsoJitProvisionFinishSetKeyConnectorKeyRequestModel.AcceptOrganizationInvite"/> is true.
    /// </summary>
    /// <remarks>
    /// Accepts the same V2 and V1 key shapes as <c>accounts/set-key-connector-key</c>.
    /// </remarks>
    [HttpPost("key-connector")]
    public async Task FinishKeyConnectorAsync([FromBody] SsoJitProvisionFinishSetKeyConnectorKeyRequestModel model)
    {
        var user = await _userService.GetUserByPrincipalAsync(User) ?? throw new UnauthorizedAccessException();

        var keys = model.KeyConnectorKeys;
        if (keys.IsV2Request())
        {
            await _setKeyConnectorKeyCommand.SetKeyConnectorKeyForUserAsync(user, keys.ToKeyConnectorKeysData(),
                model.AcceptOrganizationInvite);
            return;
        }

        // TODO remove V1 with https://bitwarden.atlassian.net/browse/PM-27328
        var result = await _userService.SetKeyConnectorKeyAsync(keys.ToUser(user), keys.Key!, keys.OrgIdentifier,
            model.AcceptOrganizationInvite);
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
