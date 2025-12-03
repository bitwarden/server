using Bit.Core.KeyManagement.Commands.Interfaces;
using Bit.Core.KeyManagement.Entities;
using Bit.Core.KeyManagement.Models.Api.Request;
using Bit.Core.KeyManagement.Repositories;
using Bit.Core.Repositories;
using Bit.Core.Utilities;

namespace Bit.Core.KeyManagement.Commands;

public class SetAccountKeysForUserCommand : ISetAccountKeysForUserCommand
{
    private readonly IUserRepository _userRepository;
    private readonly IUserSignatureKeyPairRepository _userSignatureKeyPairRepository;
    public SetAccountKeysForUserCommand(
        IUserRepository userRepository,
        IUserSignatureKeyPairRepository userSignatureKeyPairRepository)
    {
        _userRepository = userRepository;
        _userSignatureKeyPairRepository = userSignatureKeyPairRepository;
    }

    public async Task SetAccountKeysForUserAsync(Guid userId, AccountKeysRequestModel accountKeys)
    {
        var user = await _userRepository.GetByIdAsync(userId);
        if (user == null)
        {
            throw new ArgumentException("User not found", nameof(userId));
        }

        var accountKeysData = accountKeys.ToAccountKeysData();

        // Update the public key encryption key pair data
        user.PrivateKey = accountKeysData.PublicKeyEncryptionKeyPairData.WrappedPrivateKey;
        user.PublicKey = accountKeysData.PublicKeyEncryptionKeyPairData.PublicKey;
        user.RevisionDate = user.AccountRevisionDate = DateTime.UtcNow;
        // Update the signature key pair data
        if (accountKeysData.SignatureKeyPairData != null && accountKeysData.SecurityStateData != null)
        {
            user.SignedPublicKey = accountKeysData.PublicKeyEncryptionKeyPairData.SignedPublicKey;
            user.SecurityState = accountKeysData.SecurityStateData.SecurityState;
            user.SecurityVersion = accountKeysData.SecurityStateData.SecurityVersion;
            await _userSignatureKeyPairRepository.CreateAsync(new UserSignatureKeyPair
            {
                Id = CoreHelpers.GenerateComb(),
                UserId = userId,
                SignatureAlgorithm = accountKeysData.SignatureKeyPairData.SignatureAlgorithm,
                SigningKey = accountKeysData.SignatureKeyPairData.WrappedSigningKey,
                VerifyingKey = accountKeysData.SignatureKeyPairData.VerifyingKey,
                CreationDate = DateTime.UtcNow,
                RevisionDate = DateTime.UtcNow,
            });
        }
        await _userRepository.ReplaceAsync(user);
    }
}
