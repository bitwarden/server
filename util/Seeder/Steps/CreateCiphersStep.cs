using Bit.Core.Entities;
using Bit.Core.Vault.Entities;
using Bit.Core.Vault.Enums;
using Bit.Seeder.Data.Distributions;
using Bit.Seeder.Factories;
using Bit.Seeder.Models;
using Bit.Seeder.Pipeline;

namespace Bit.Seeder.Steps;

/// <summary>
/// Loads cipher items from a fixture and creates encrypted cipher entities.
/// Supports both organization ciphers (encrypted with org key, assigned to collections)
/// and personal ciphers (encrypted with user key, no collections).
/// </summary>
internal sealed class CreateCiphersStep : IStep
{
    private readonly string _fixtureName;
    private readonly bool _skipCollectionAssignment;
    private readonly bool _personal;

    private CreateCiphersStep(string fixtureName, bool skipCollectionAssignment, bool personal)
    {
        _fixtureName = fixtureName;
        _skipCollectionAssignment = skipCollectionAssignment;
        _personal = personal;
    }

    internal static CreateCiphersStep ForOrganization(string fixtureName, bool skipCollectionAssignment = false) =>
        new(fixtureName, skipCollectionAssignment, personal: false);

    internal static CreateCiphersStep ForPersonalVault(string fixtureName) =>
        new(fixtureName, skipCollectionAssignment: true, personal: true);

    public void Execute(SeederContext context)
    {
        string encryptionKey;
        Guid? organizationId = null;
        Guid? userId = null;

        if (_personal)
        {
            var userDigest = context.Registry.UserDigests[0];
            encryptionKey = userDigest.SymmetricKey;
            userId = userDigest.UserId;
        }
        else
        {
            organizationId = context.RequireOrgId();
            encryptionKey = context.RequireOrgKey();
        }

        var seedFile = context.GetSeedReader().Read<SeedFile>($"ciphers.{_fixtureName}");
        var collectionIds = context.Registry.CollectionIds;
        var progress = context.GetProgress();

        var ciphers = new List<Cipher>(seedFile.Items.Count);
        var collectionCiphers = new List<CollectionCipher>();

        progress?.Report(new PhaseStarted(SeederPhases.CreatingCiphers, seedFile.Items.Count));
        var ticker = new ProgressTicker(progress, SeederPhases.CreatingCiphers, seedFile.Items.Count);

        // Per-item archived/deleted lifecycle state on fixture ciphers. Required because the generated-cipher
        // path applies archive/delete only via density rates, and generated ciphers can never carry attachments
        // (attachments are fixture-only) — so archived/deleted ciphers that ALSO have attachments are only
        // expressible here, on named fixture items. Archive is per-user (attributed to the vault owner, so it
        // renders as archived in the owner's vault); delete is a global soft-delete. Reuses the same
        // CipherComposer logic as the generated path so CreationDate/RevisionDate are backdated consistently.
        var lifecycleSets = BuildLifecycleSets(seedFile.Items);
        var hasLifecycleState = lifecycleSets.Archived.Count > 0 || lifecycleSets.DeletedOnly.Count > 0;
        Guid ResolveArchiveOwner(int _) => _personal
            ? userId!.Value
            : context.Owner?.Id ?? throw new InvalidOperationException(
                "A fixture cipher is marked archived, but the organization has no owner to attribute the archive to. " +
                "Provide a roster owner or call AddOwner() before creating ciphers.");

        for (var i = 0; i < seedFile.Items.Count; i++)
        {
            var item = seedFile.Items[i];
            var options = CipherSeed.FromSeedItem(item) with
            {
                EncryptionKey = encryptionKey,
                OrganizationId = organizationId,
                UserId = userId
            };
            options.Validate();
            var cipher = options.Type switch
            {
                CipherType.Login => LoginCipherSeeder.Create(options),
                CipherType.Card => CardCipherSeeder.Create(options),
                CipherType.Identity => IdentityCipherSeeder.Create(options),
                CipherType.SecureNote => SecureNoteCipherSeeder.Create(options),
                CipherType.SSHKey => SshKeyCipherSeeder.Create(options),
                CipherType.BankAccount => BankAccountCipherSeeder.Create(options),
                CipherType.DriversLicense => DriversLicenseCipherSeeder.Create(options),
                CipherType.Passport => PassportCipherSeeder.Create(options),
                _ => throw new ArgumentOutOfRangeException(nameof(options), $"Unsupported cipher type: {options.Type}")
            };

            if (item.Favorite == true && userId.HasValue)
            {
                cipher.Favorites = CipherComposer.BuildFavoritesJson([userId.Value]);
            }

            cipher.Reprompt = options.Reprompt;

            if (hasLifecycleState)
            {
                CipherComposer.AssignArchiveOrDeleteState(cipher, i, lifecycleSets, ResolveArchiveOwner);
            }

            ciphers.Add(cipher);

            if (context.Registry.FixtureCipherNameToId.ContainsKey(item.Name))
            {
                throw new InvalidOperationException(
                    $"Duplicate cipher name '{item.Name}' in fixture '{_fixtureName}'. " +
                    "Cipher names must be unique for folder and favorite assignments.");
            }

            context.Registry.FixtureCipherNameToId[item.Name] = cipher.Id;

            ticker.Tick();

            // Collection assignment (round-robin, skipped for personal vaults or when collectionAssignments handles it)
            if (_skipCollectionAssignment || collectionIds.Count <= 0)
            {
                continue;
            }

            collectionCiphers.Add(new CollectionCipher
            {
                CipherId = cipher.Id,
                CollectionId = collectionIds[i % collectionIds.Count]
            });

            // Every 3rd cipher gets assigned to an additional collection
            if (i % 3 == 0 && collectionIds.Count > 1)
            {
                collectionCiphers.Add(new CollectionCipher
                {
                    CipherId = cipher.Id,
                    CollectionId = collectionIds[(i + 1) % collectionIds.Count]
                });
            }
        }

        ticker.Flush();

        context.Ciphers.AddRange(ciphers);
        context.CollectionCiphers.AddRange(collectionCiphers);

        progress?.Report(new PhaseCompleted(SeederPhases.CreatingCiphers));
    }

    /// <summary>
    /// Maps each item's <c>archived</c>/<c>deleted</c> flags onto the index sets
    /// <see cref="CipherComposer.AssignArchiveOrDeleteState"/> consumes. <c>Both</c> is the subset that is
    /// both archived and deleted; <c>DeletedOnly</c> is deleted but not archived.
    /// </summary>
    private static ArchiveDeleteSets BuildLifecycleSets(IReadOnlyList<SeedVaultItem> items)
    {
        var archived = new HashSet<int>();
        var both = new HashSet<int>();
        var deletedOnly = new HashSet<int>();

        for (var i = 0; i < items.Count; i++)
        {
            var isArchived = items[i].Archived == true;
            var isDeleted = items[i].Deleted == true;

            if (isArchived)
            {
                archived.Add(i);
                if (isDeleted)
                {
                    both.Add(i);
                }
            }
            else if (isDeleted)
            {
                deletedOnly.Add(i);
            }
        }

        return new ArchiveDeleteSets(archived, both, deletedOnly, archived.OrderBy(index => index).ToList());
    }
}
