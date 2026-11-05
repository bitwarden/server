using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.UpdateUserResetPasswordEnrollment;
using Bit.Core.Auth.Enums;
using Bit.Core.Auth.Repositories;
using Bit.Core.Auth.UserFeatures.DeviceTrust.Data;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.OrganizationFeatures.OrganizationUsers.Interfaces;
using Bit.Core.Repositories;
using Bit.Core.Services;

namespace Bit.Core.Auth.UserFeatures.DeviceTrust;

public class FinishSsoJitProvisionTrustedDeviceCommand(
    IUserRepository userRepository,
    IUserService userService,
    ISsoConfigRepository ssoConfigRepository,
    IOrganizationUserRepository organizationUserRepository,
    IDeviceRepository deviceRepository,
    IDeviceService deviceService,
    IUpdateUserResetPasswordEnrollmentCommand updateUserResetPasswordEnrollmentCommand,
    IAcceptOrgUserCommand acceptOrgUserCommand) : IFinishSsoJitProvisionTrustedDeviceCommand
{
    private readonly IUserRepository _userRepository = userRepository;
    private readonly IUserService _userService = userService;
    private readonly ISsoConfigRepository _ssoConfigRepository = ssoConfigRepository;
    private readonly IOrganizationUserRepository _organizationUserRepository = organizationUserRepository;
    private readonly IDeviceRepository _deviceRepository = deviceRepository;
    private readonly IDeviceService _deviceService = deviceService;
    private readonly IUpdateUserResetPasswordEnrollmentCommand _updateUserResetPasswordEnrollmentCommand =
        updateUserResetPasswordEnrollmentCommand;
    private readonly IAcceptOrgUserCommand _acceptOrgUserCommand = acceptOrgUserCommand;

    /// <inheritdoc/>
    public async Task FinishProvisionAsync(
        User user,
        FinishSsoJitProvisionTrustedDeviceData data,
        bool acceptOrganizationInvite = true)
    {
        // All checks run before the first write: once the keypair is stored, a client retry is rejected.
        if (!string.IsNullOrWhiteSpace(user.PrivateKey) || !string.IsNullOrWhiteSpace(user.PublicKey))
        {
            throw new BadRequestException("User has existing keypair");
        }

        // Enrollment below skips the master password check, which is only safe for trusted device encryption.
        var ssoConfig = await _ssoConfigRepository.GetByOrganizationIdAsync(data.OrganizationId);
        if (ssoConfig is not { Enabled: true } ||
            ssoConfig.GetData().MemberDecryptionType != MemberDecryptionType.TrustedDeviceEncryption)
        {
            throw new BadRequestException("Organization does not use trusted device encryption.");
        }

        var orgUser = await _organizationUserRepository.GetByOrganizationAsync(data.OrganizationId, user.Id)
            ?? throw new BadRequestException("User not found within organization.");

        // A missing key would make the enrollment command withdraw instead of enroll.
        // For now, account recovery enrollment follows AcceptOrganizationInvite.
        if (acceptOrganizationInvite && !OrganizationUser.IsValidResetPasswordKey(data.ResetPasswordKey))
        {
            throw new BadRequestException("A reset password key is required to accept the organization invite.");
        }

        Device? device = null;
        if (data.DeviceKeys != null)
        {
            device = await _deviceRepository.GetByIdentifierAsync(data.DeviceKeys.DeviceIdentifier, user.Id)
                ?? throw new NotFoundException();
        }

        await SetAccountKeysAsync(user, data);

        if (device != null)
        {
            device.EncryptedUserKey = data.DeviceKeys!.EncryptedUserKey;
            device.EncryptedPublicKey = data.DeviceKeys.EncryptedPublicKey;
            device.EncryptedPrivateKey = data.DeviceKeys.EncryptedPrivateKey;
            await _deviceService.SaveAsync(device);
        }

        // For now, account recovery enrollment follows AcceptOrganizationInvite.
        if (!acceptOrganizationInvite)
        {
            return;
        }

        await _updateUserResetPasswordEnrollmentCommand.UpdateUserResetPasswordEnrollmentAsync(
            data.OrganizationId, user.Id, data.ResetPasswordKey, user.Id);

        if (orgUser.Status == OrganizationUserStatusType.Invited)
        {
            // Accept by org id so the membership is re-read; the copy loaded above predates the enrollment
            // write and would overwrite the stored reset password key.
            await _acceptOrgUserCommand.AcceptOrgUserByOrgIdAsync(data.OrganizationId, user, _userService);
        }
    }

    private async Task SetAccountKeysAsync(User user, FinishSsoJitProvisionTrustedDeviceData data)
    {
        if (data.AccountKeys.IsV2Encryption())
        {
            var updateUserDataTasks = data.UserKeyId == null
                ? null
                : new[] { _userRepository.SetUserKeyId(user.Id, data.UserKeyId) };

            await _userRepository.SetV2AccountCryptographicStateAsync(user.Id, data.AccountKeys,
                updateUserDataTasks);
            return;
        }

        // TODO: Drop together with the V1 path of AccountsController.PostKeys.
        // https://bitwarden.atlassian.net/browse/PM-27329
        user.PublicKey = data.AccountKeys.PublicKeyEncryptionKeyPairData.PublicKey;
        user.PrivateKey = data.AccountKeys.PublicKeyEncryptionKeyPairData.WrappedPrivateKey;
        await _userService.SaveUserAsync(user);
    }
}
