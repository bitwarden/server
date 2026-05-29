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
    DensityProfile? density = null,
    int repromptEveryNthCipher = 0) : IStep
{
    public void Execute(SeederContext context)
    {
        if (countPerUser == 0 && density?.PersonalCipherDistribution is null)
        {
            return;
        }

        var generator = context.RequireGenerator();
        var progress = context.GetProgress();

        var userDigests = context.Registry.UserDigests;
        var typeDistribution = typeDist ?? CipherTypeDistributions.Realistic;
        var passwordDistribution = pwDist ?? PasswordDistributions.Realistic;
        var companies = Companies.All;
        var personalDist = density?.PersonalCipherDistribution;
        var userFolderIds = context.Registry.UserFolderIds;
        var expectedTotal = personalDist is not null
            ? EstimateTotal(userDigests.Count, personalDist)
            : userDigests.Count * countPerUser;

        // Force lazy generator init before parallel loop (prevents ??= data race)
        _ = (generator.Username, generator.Card, generator.Identity, generator.SecureNote);

        // Pre-compute per-user counts and globalIndex offsets
        var userCounts = new int[userDigests.Count];
        var offsets = new int[userDigests.Count];
        var runningOffset = 0;

        for (var u = 0; u < userDigests.Count; u++)
        {
            var userCount = countPerUser;
            if (personalDist is not null)
            {
                var range = personalDist.Select(u, userDigests.Count);
                userCount = range.Min + (u % Math.Max(range.Max - range.Min + 1, 1));
            }

            userCounts[u] = userCount;
            offsets[u] = runningOffset;
            runningOffset += userCount;
        }

        var userCiphers = new Cipher[userDigests.Count][];

        progress?.Report(new PhaseStarted(SeederPhases.CreatingPersonalCiphers, expectedTotal));
        var batchSize = Math.Max(1, expectedTotal / 100);

        Parallel.For(
            0,
            userDigests.Count,
            new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount },
            localInit: () => 0,
            body: (u, _, localTicked) =>
            {
                var userDigest = userDigests[u];
                var localCount = userCounts[u];
                var baseOffset = offsets[u];
                var localCiphers = new Cipher[localCount];

                for (var i = 0; i < localCount; i++)
                {
                    var globalIndex = baseOffset + i;
                    var cipherType = typeDistribution.Select(globalIndex, expectedTotal);
                    var reprompt = repromptEveryNthCipher > 0 && globalIndex % repromptEveryNthCipher == 0
                        ? CipherRepromptType.Password
                        : CipherRepromptType.None;
                    var cipher = CipherComposer.Compose(globalIndex, cipherType, userDigest.SymmetricKey, companies, generator, passwordDistribution, userId: userDigest.UserId, reprompt: reprompt);

                    CipherComposer.AssignFolder(cipher, userDigest.UserId, i, userFolderIds);

                    localCiphers[i] = cipher;
                }

                userCiphers[u] = localCiphers;

                localTicked += localCount;
                if (progress is not null && localTicked >= batchSize)
                {
                    progress.Report(new PhaseAdvanced(SeederPhases.CreatingPersonalCiphers, localTicked));
                    localTicked = 0;
                }
                return localTicked;
            },
            localFinally: localTicked =>
            {
                if (progress is not null && localTicked > 0)
                {
                    progress.Report(new PhaseAdvanced(SeederPhases.CreatingPersonalCiphers, localTicked));
                }
            });

        // Flatten jagged array into context lists
        var ciphers = new List<Cipher>(expectedTotal);
        var cipherIds = new List<Guid>(expectedTotal);

        for (var u = 0; u < userDigests.Count; u++)
        {
            var localCiphers = userCiphers[u];
            for (var i = 0; i < localCiphers.Length; i++)
            {
                ciphers.Add(localCiphers[i]);
                cipherIds.Add(localCiphers[i].Id);
            }
        }

        context.Ciphers.AddRange(ciphers);
        context.Registry.CipherIds.AddRange(cipherIds);

        progress?.Report(new PhaseCompleted(SeederPhases.CreatingPersonalCiphers));
    }

    private static int EstimateTotal(int userCount, Distribution<(int Min, int Max)> dist)
    {
        var total = 0;
        for (var i = 0; i < userCount; i++)
        {
            var range = dist.Select(i, userCount);
            total += range.Min + (i % Math.Max(range.Max - range.Min + 1, 1));
        }

        return Math.Max(total, 1);
    }
}
