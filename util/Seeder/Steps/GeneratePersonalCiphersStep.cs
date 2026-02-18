using Bit.Core.Vault.Entities;
using Bit.Core.Vault.Enums;
using Bit.Seeder.Data.Distributions;
using Bit.Seeder.Data.Enums;
using Bit.Seeder.Data.Static;
using Bit.Seeder.Factories;
using Bit.Seeder.Pipeline;

namespace Bit.Seeder.Steps;

/// <summary>
/// Creates N personal cipher entities per user, encrypted with each user's symmetric key.
/// </summary>
/// <remarks>
/// Iterates over <see cref="EntityRegistry.UserDigests"/> and creates ciphers with
/// <c>UserId</c> set and <c>OrganizationId</c> null. Personal ciphers are not assigned
/// to collections.
/// </remarks>
internal sealed class GeneratePersonalCiphersStep(
    int countPerUser,
    Distribution<CipherType>? typeDist = null,
    Distribution<PasswordStrength>? pwDist = null) : IStep
{
    public void Execute(SeederContext context)
    {
        if (countPerUser == 0)
        {
            return;
        }

        var generator = context.RequireGenerator();

        var userDigests = context.Registry.UserDigests;
        var typeDistribution = typeDist ?? CipherTypeDistributions.Realistic;
        var passwordDistribution = pwDist ?? PasswordDistributions.Realistic;
        var companies = Companies.All;

        var ciphers = new List<Cipher>(userDigests.Count * countPerUser);
        var cipherIds = new List<Guid>(userDigests.Count * countPerUser);
        var globalIndex = 0;

        foreach (var userDigest in userDigests)
        {
            for (var i = 0; i < countPerUser; i++)
            {
                var cipherType = typeDistribution.Select(globalIndex, userDigests.Count * countPerUser);
                var cipher = cipherType switch
                {
                    CipherType.Login => CipherComposer.ComposeLogin(globalIndex, userDigest.SymmetricKey, companies, generator, passwordDistribution, userId: userDigest.UserId),
                    CipherType.Card => CipherComposer.ComposeCard(globalIndex, userDigest.SymmetricKey, generator, userId: userDigest.UserId),
                    CipherType.Identity => CipherComposer.ComposeIdentity(globalIndex, userDigest.SymmetricKey, generator, userId: userDigest.UserId),
                    CipherType.SecureNote => CipherComposer.ComposeSecureNote(globalIndex, userDigest.SymmetricKey, generator, userId: userDigest.UserId),
                    CipherType.SSHKey => CipherComposer.ComposeSshKey(globalIndex, userDigest.SymmetricKey, userId: userDigest.UserId),
                    _ => throw new ArgumentException($"Unsupported cipher type: {cipherType}")
                };

                CipherComposer.AssignFolder(cipher, userDigest.UserId, i, context.Registry.UserFolderIds);

                ciphers.Add(cipher);
                cipherIds.Add(cipher.Id);
                globalIndex++;
            }
        }

        context.Ciphers.AddRange(ciphers);
        context.Registry.CipherIds.AddRange(cipherIds);
    }
}
