using Bit.Api.Auth.Models.Request.SsoJitProvisionFinish;
using Bit.Core;
using Bit.Core.Auth.Identity;
using Bit.Core.Auth.UserFeatures.UserMasterPassword.Interfaces;
using Bit.Core.Entities;
using Bit.Core.Exceptions;
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
    ISetInitialMasterPasswordCommandV1 setInitialMasterPasswordCommandV1) : Controller
{
    private readonly IUserService _userService = userService;
    private readonly Bitwarden.Server.Sdk.Features.IFeatureService _featureService = featureService;
    private readonly IFinishSsoJitProvisionMasterPasswordCommand _finishSsoJitProvisionMasterPasswordCommand =
        finishSsoJitProvisionMasterPasswordCommand;
    private readonly ISetInitialMasterPasswordCommandV1 _setInitialMasterPasswordCommandV1 = setInitialMasterPasswordCommandV1;

    /// <summary>
    /// Sets the initial master password and account keys for an SSO JIT provisioned user in an organization
    /// that uses master password decryption. Accepts the pending organization invite only when
    /// <see cref="SsoJitProvisionFinishSetInitialPasswordRequestModel.AcceptOrganizationInvite"/> is <c>true</c>.
    /// </summary>
    /// <remarks>
    /// Accepts MPAD + MPUD with exactly one keypair: V2 encryption (AccountKeys, V2 MP JIT flag on)
    /// or V1 encryption (legacy Keys).
    /// </remarks>
    [HttpPost("password")]
    public async Task FinishMasterPasswordAsync([FromBody] SsoJitProvisionFinishSetInitialPasswordRequestModel model)
    {
        var user = await _userService.GetUserByPrincipalAsync(User) ?? throw new UnauthorizedAccessException();

        // Require AccountKeys so V1 requests (legacy Keys) still route to V1 when the flag is on.
        if (model.AccountKeys != null &&
            _featureService.IsEnabled(FeatureFlagKeys.EnableAccountEncryptionV2JitPasswordRegistration))
        {
            await _finishSsoJitProvisionMasterPasswordCommand.FinishProvisionAsync(
                user, model.ToData(), model.AcceptOrganizationInvite);
            return;
        }

        await SetInitialPasswordV1Async(user, model);
    }

    /// <summary>
    /// V1 encryption path. Mirrors <c>AccountsController.SetInitialPasswordV1Async</c> with option of not accepting
    /// the organization invite. Remove with it once the conditions documented there are met.
    /// </summary>
    private async Task SetInitialPasswordV1Async(
        User user,
        SsoJitProvisionFinishSetInitialPasswordRequestModel model)
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
}
