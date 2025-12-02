using Bit.Core.KeyManagement.Commands.Interfaces;
using Bit.Core.KeyManagement.Entities;
using Bit.Core.KeyManagement.Models.Api.Request;
using Bit.Core.KeyManagement.Repositories;
using Bit.Core.Repositories;
using Bit.Core.Utilities;

namespace Bit.Core.KeyManagement.Commands;

public class SetAccountKeysForUserCommand : ISetAccountKeysForUserCommand
{
    public async Task SetAccountKeysForUserAsync(Guid userId, AccountKeysRequestModel accountKeys, IUserRepository userRepository, IUserSignatureKeyPairRepository userSignatureKeyPairRepository)
    {
        var user = await userRepository.GetByIdAsync(userId);
        if (user == null)
        {
            throw new ArgumentException("User not found", nameof(userId));
        }

        var accountKeysData = accountKeys.ToAccountKeysData();

        // Update the public key encryption key pair data
        user.PrivateKey = accountKeysData.PublicKeyEncryptionKeyPairData.WrappedPrivateKey;
        user.PublicKey = accountKeysData.PublicKeyEncryptionKeyPairData.PublicKey;
        user.RevisionDate = user.AccountRevisionDate = DateTime.UtcNow;
        await userRepository.ReplaceAsync(user);
        // Update the signature key pair data
        if (accountKeysData.SignatureKeyPairData != null)
        {
            await userSignatureKeyPairRepository.UpsertAsync(new UserSignatureKeyPair
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
    }
}
