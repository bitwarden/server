using Bit.Core.Entities;
using Bit.Core.Exceptions;
using Bit.Core.KeyManagement.Models.Data;
using Bit.Core.Platform.Push;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Microsoft.AspNetCore.Identity;

namespace Bit.Core.KeyManagement.Kdf.Implementations;

/// <inheritdoc />
public class ChangeKdfCommand : IChangeKdfCommand
{
    private readonly IUserService _userService;
    private readonly IPushNotificationService _pushService;
    private readonly IUserRepository _userRepository;
    private readonly IdentityErrorDescriber _identityErrorDescriber;

    public ChangeKdfCommand(IUserService userService, IPushNotificationService pushService, IUserRepository userRepository, IdentityErrorDescriber describer)
    {
        _userService = userService;
        _pushService = pushService;
        _userRepository = userRepository;
        _identityErrorDescriber = describer;
    }

    public async Task<IdentityResult> ChangeKdfAsync(User user, string masterPasswordAuthenticationHash, MasterPasswordAuthenticationData authenticationData, MasterPasswordUnlockData unlockData)
    {
        if (user == null)
        {
            throw new ArgumentNullException(nameof(user));
        }
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
        var result = await _userService.UpdatePasswordHash(user, authenticationData.MasterPasswordAuthenticationHash);
        if (!result.Succeeded)
        {
            return result;
        }
        // Salt is ensured to be the same as unlock data, and the value stored in the account and not updated.
        // KDF is ensured to be the same as unlock data above and updated below.

        user.Key = unlockData.MasterKeyWrappedUserKey;
        // Salt is ensured to be the same as authentication data, and the value stored in the account, and is not updated.
        // Kdf - These will be seperated in the future, but for now are ensured to be the same as authentication data above.
        user.Kdf = unlockData.Kdf.KdfType;
        user.KdfIterations = unlockData.Kdf.Iterations;
        user.KdfMemory = unlockData.Kdf.Memory;
        user.KdfParallelism = unlockData.Kdf.Parallelism;

        var now = DateTime.UtcNow;
        user.RevisionDate = user.AccountRevisionDate = now;
        user.LastKdfChangeDate = now;

        await _userRepository.ReplaceAsync(user);
        await _pushService.PushLogOutAsync(user.Id);
        return IdentityResult.Success;
    }
}
