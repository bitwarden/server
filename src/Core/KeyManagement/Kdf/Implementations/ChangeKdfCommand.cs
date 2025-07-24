#nullable enable

using Bit.Core.Entities;
using Bit.Core.Exceptions;
using Bit.Core.KeyManagement.Kdf;
using Bit.Core.KeyManagement.Models.Data;
using Bit.Core.Platform.Push;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Microsoft.AspNetCore.Identity;

namespace Bit.Core.KeyManagement.UserKey.Implementations;

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

    public async Task<IdentityResult> ChangeKdfAsync(User user, string masterPasswordAuthenticationHash, string newMasterPasswordAuthenticationHash,
        string masterKeyWrappedUserKey, KdfSettings kdf, MasterPasswordAuthenticationData? authenticationData, MasterPasswordUnlockData? unlockData)
    {
        if (user == null)
        {
            throw new ArgumentNullException(nameof(user));
        }

        if (authenticationData != null && unlockData != null)
        {
            // Prevent a de-synced salt value from creating an un-decryptable unlock method 
            authenticationData.ValidateSaltUnchangedForUser(user);
            unlockData.ValidateSaltUnchangedForUser(user);

            // Currently KDF settings are not saved separately for authentication and unlock and must therefore be equal
            if (!authenticationData.Kdf.Equals(unlockData.Kdf))
            {
                throw new BadRequestException("KDF settings must be equal for authentication and unlock.");
            }

            // If both authentication and unlock data are present, use them instead of the deprecated values.
            kdf = authenticationData.Kdf;
            newMasterPasswordAuthenticationHash = authenticationData.MasterPasswordAuthenticationHash;
            masterKeyWrappedUserKey = unlockData.MasterKeyWrappedUserKey;
        }


        if (await _userService.CheckPasswordAsync(user, masterPasswordAuthenticationHash))
        {
            var result = await _userService.UpdatePasswordHash(user, newMasterPasswordAuthenticationHash);
            if (!result.Succeeded)
            {
                return result;
            }

            var now = DateTime.UtcNow;
            user.RevisionDate = user.AccountRevisionDate = now;
            user.LastKdfChangeDate = now;
            user.Key = masterKeyWrappedUserKey;
            user.Kdf = kdf.KdfType;
            user.KdfIterations = kdf.Iterations;
            user.KdfMemory = kdf.Memory;
            user.KdfParallelism = kdf.Parallelism;
            await _userRepository.ReplaceAsync(user);
            await _pushService.PushLogOutAsync(user.Id);
            return IdentityResult.Success;
        }

        return IdentityResult.Failed(_identityErrorDescriber.PasswordMismatch());
    }

}
