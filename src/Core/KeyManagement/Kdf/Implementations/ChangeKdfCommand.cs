using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.KeyManagement.Models.Data;
using Bit.Core.Platform.Push;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Utilities;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

namespace Bit.Core.KeyManagement.Kdf.Implementations;

/// <inheritdoc />
public class ChangeKdfCommand : IChangeKdfCommand
{
    private readonly IUserService _userService;
    private readonly IPushNotificationService _pushService;
    private readonly IUserRepository _userRepository;
    private readonly IdentityErrorDescriber _identityErrorDescriber;
    private readonly ILogger<ChangeKdfCommand> _logger;
    private readonly IFeatureService _featureService;

    public ChangeKdfCommand(IUserService userService, IPushNotificationService pushService,
        IUserRepository userRepository, IdentityErrorDescriber describer, ILogger<ChangeKdfCommand> logger,
        IFeatureService featureService)
    {
        _userService = userService;
        _pushService = pushService;
        _userRepository = userRepository;
        _identityErrorDescriber = describer;
        _logger = logger;
        _featureService = featureService;
    }

    public async Task<IdentityResult> ChangeKdfAsync(User user, string masterPasswordAuthenticationHash,
        MasterPasswordAuthenticationData authenticationData, MasterPasswordUnlockData unlockData)
    {
        ArgumentNullException.ThrowIfNull(user);
        if (!await _userService.CheckPasswordAsync(user, masterPasswordAuthenticationHash))
        {
            return IdentityResult.Failed(_identityErrorDescriber.PasswordMismatch());
        }

        // Validate to prevent user account from becoming un-decryptable from invalid parameters
        //
        // Prevent a de-synced salt value from creating an un-decryptable unlock method
        authenticationData.ValidateSaltUnchangedForUser(user);
        unlockData.ValidateSaltUnchangedForUser(user);

        // Currently KDF settings are not saved separately for authentication and unlock and must therefore be equal
        if (!authenticationData.Kdf.Equals(unlockData.Kdf))
        {
            throw new BadRequestException("KDF settings must be equal for authentication and unlock.");
        }

        var validationErrors = KdfSettingsValidator.Validate(unlockData.Kdf);
        if (validationErrors.Any())
        {
            throw new BadRequestException("KDF settings are invalid.");
        }

        var logoutOnKdfChange = !_featureService.IsEnabled(FeatureFlagKeys.NoLogoutOnKdfChange);

        // Update the user with the new KDF settings
        // This updates the authentication data and unlock data for the user separately. Currently these still
        // use shared values for KDF settings and salt.
        // The authentication hash, and the unlock data each are dependent on:
        // - The master password (entered by the user every time)
        // - The KDF settings (iterations, memory, parallelism)
        // - The salt
        // These combinations - (password, authentication hash, KDF settings, salt) and (password, unlock data, KDF settings, salt)
        // must remain consistent to unlock correctly.

        // Authentication
        // Note: This mutates the user but does not yet save it to DB. That is done atomically, later.
        // This entire operation MUST be atomic to prevent a user from being locked out of their account.
        // Salt is ensured to be the same as unlock data, and the value stored in the account and not updated.
        // KDF is ensured to be the same as unlock data above and updated below.
        var result = await _userService.UpdatePasswordHash(user, authenticationData.MasterPasswordAuthenticationHash,
            refreshStamp: logoutOnKdfChange);
        if (!result.Succeeded)
        {
            _logger.LogWarning("Change KDF failed for user {userId}.", user.Id);
            return result;
        }

        // Salt is ensured to be the same as authentication data, and the value stored in the account, and is not updated.
        // Kdf - These will be seperated in the future, but for now are ensured to be the same as authentication data above.
        user.Key = unlockData.MasterKeyWrappedUserKey;
        user.Kdf = unlockData.Kdf.KdfType;
        user.KdfIterations = unlockData.Kdf.Iterations;
        user.KdfMemory = unlockData.Kdf.Memory;
        user.KdfParallelism = unlockData.Kdf.Parallelism;

        var now = DateTime.UtcNow;
        user.RevisionDate = user.AccountRevisionDate = now;
        user.LastKdfChangeDate = now;

        await _userRepository.ReplaceAsync(user);
        if (logoutOnKdfChange)
        {
            await _pushService.PushLogOutAsync(user.Id);
        }
        else
        {
            // Clients that support the new feature flag will ignore the logout when it matches the reason and the feature flag is enabled.
            await _pushService.PushLogOutAsync(user.Id, reason: PushNotificationLogOutReason.KdfChange);
            await _pushService.PushSyncSettingsAsync(user.Id);
        }

        return IdentityResult.Success;
    }
}
