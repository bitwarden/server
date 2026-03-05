using Bit.Core.Vault.Entities;
using Bit.Core.Vault.Enums;
using Bit.Seeder.Data.Distributions;
using Bit.Seeder.Data.Enums;
using Bit.Seeder.Data.Static;
using Bit.Seeder.Factories;
using Bit.Seeder.Options;
using Bit.Seeder.Pipeline;

namespace Bit.Seeder.Steps;

/// <summary>
/// Creates personal cipher entities per user, encrypted with each user's symmetric key.
/// </summary>
/// <remarks>
/// Iterates over <see cref="EntityRegistry.UserDigests"/> and creates ciphers with
/// <c>UserId</c> set and <c>OrganizationId</c> null. Personal ciphers are not assigned
/// to collections. When a <see cref="DensityProfile.PersonalCipherDistribution"/> is set,
/// each user's count varies according to the distribution instead of using a flat count.
/// </remarks>
internal sealed class GeneratePersonalCiphersStep(
    int countPerUser,
    Distribution<CipherType>? typeDist = null,
    Distribution<PasswordStrength>? pwDist = null,
    DensityProfile? density = null) : IStep
{
    public void Execute(SeederContext context)
    {
        if (countPerUser == 0 && density?.PersonalCipherDistribution is null)
        {
            return;
        }

        var generator = context.RequireGenerator();

        var userDigests = context.Registry.UserDigests;
        var typeDistribution = typeDist ?? CipherTypeDistributions.Realistic;
        var passwordDistribution = pwDist ?? PasswordDistributions.Realistic;
        var companies = Companies.All;
        var personalDist = density?.PersonalCipherDistribution;
        var expectedTotal = personalDist is not null
            ? EstimateTotal(userDigests.Count, personalDist)
            : userDigests.Count * countPerUser;

        var ciphers = new List<Cipher>(expectedTotal);
        var cipherIds = new List<Guid>(expectedTotal);
        var globalIndex = 0;

        for (var userIndex = 0; userIndex < userDigests.Count; userIndex++)
        {
            var userDigest = userDigests[userIndex];
            var userCount = countPerUser;
            if (personalDist is not null)
            {
                var range = personalDist.Select(userIndex, userDigests.Count);
                userCount = range.Min + (userIndex % Math.Max(range.Max - range.Min, 1));
            }

            for (var i = 0; i < userCount; i++)
            {
                var cipherType = typeDistribution.Select(globalIndex, expectedTotal);
                var cipher = CipherComposer.Compose(globalIndex, cipherType, userDigest.SymmetricKey, companies, generator, passwordDistribution, userId: userDigest.UserId);

                CipherComposer.AssignFolder(cipher, userDigest.UserId, i, context.Registry.UserFolderIds);

                ciphers.Add(cipher);
                cipherIds.Add(cipher.Id);
                globalIndex++;
            }
        }

        context.Ciphers.AddRange(ciphers);
        context.Registry.CipherIds.AddRange(cipherIds);
    }

    private static int EstimateTotal(int userCount, Distribution<(int Min, int Max)> dist)
    {
        var total = 0;
        for (var i = 0; i < userCount; i++)
        {
            var range = dist.Select(i, userCount);
            total += range.Min + (i % Math.Max(range.Max - range.Min, 1));
        }

        return Math.Max(total, 1);
    }
}
