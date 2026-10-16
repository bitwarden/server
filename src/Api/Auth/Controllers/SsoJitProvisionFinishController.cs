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
/// Finishes onboarding for SSO JIT provisioned users by member decryption type. These endpoints do not accept a user's 
/// pending organization invite, so clients can rely on the membership status being left unchanged.
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
    /// that uses master password decryption. Does not accept the organization membership.
    /// </summary>
    /// <remarks>
    /// Accepts the same request shapes as the MP JIT branches of <c>accounts/set-password</c>:
    /// V2 encryption (MPAD + MPUD + AccountKeys, V2 MP JIT flag on) and V1 encryption
    /// (modern or legacy shape with legacy Keys). A keypair is required.
    /// </remarks>
    [HttpPost("password")]
    public async Task FinishMasterPasswordAsync([FromBody] SsoJitProvisionFinishSetInitialPasswordRequestModel model)
    {
        var user = await _userService.GetUserByPrincipalAsync(User) ?? throw new UnauthorizedAccessException();

        if (model.HasAuthAndUnlockData() &&
            _featureService.IsEnabled(FeatureFlagKeys.EnableAccountEncryptionV2JitPasswordRegistration))
        {
            await _finishSsoJitProvisionMasterPasswordCommand.FinishProvisionAsync(user, model.ToData());
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
            model.MasterPasswordUnlock?.ToData().ValidateSaltUnchangedForUser(user);
            model.MasterPasswordAuthentication?.ToData().ValidateSaltUnchangedForUser(user);

            // ToUser() handles fallbacks if MPAD + MPUD are not present (i.e. legacy shape)
            user = model.ToUser(user);
        }
        catch (Exception e)
        {
            ModelState.AddModelError(string.Empty, e.Message);
            throw new BadRequestException(ModelState);
        }

        var result = await _setInitialMasterPasswordCommandV1.SetInitialMasterPasswordAsync(
            user,
            model.MasterPasswordAuthentication?.MasterPasswordAuthenticationHash ?? model.MasterPasswordHash!,
            model.MasterPasswordUnlock?.MasterKeyWrappedUserKey ?? model.Key!,
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
