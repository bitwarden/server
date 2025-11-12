// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyRequirements;
using Bit.Core.AdminConsole.Services;
using Bit.Core.Entities;
using Bit.Core.Exceptions;
using Bit.Core.Platform.Push;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Tools.ImportFeatures.Interfaces;
using Bit.Core.Vault.Entities;
using Bit.Core.Vault.Models.Data;
using Bit.Core.Vault.Repositories;

namespace Bit.Core.Tools.ImportFeatures;

public class ImportCiphersCommand : IImportCiphersCommand
{
    private readonly ICipherRepository _cipherRepository;
    private readonly IFolderRepository _folderRepository;
    private readonly IPushNotificationService _pushService;
    private readonly IPolicyService _policyService;
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IOrganizationUserRepository _organizationUserRepository;
    private readonly ICollectionRepository _collectionRepository;
    private readonly IPolicyRequirementQuery _policyRequirementQuery;
    private readonly IFeatureService _featureService;

    public ImportCiphersCommand(
        ICipherRepository cipherRepository,
        IFolderRepository folderRepository,
        ICollectionRepository collectionRepository,
        IOrganizationRepository organizationRepository,
        IOrganizationUserRepository organizationUserRepository,
        IPushNotificationService pushService,
        IPolicyService policyService,
        IPolicyRequirementQuery policyRequirementQuery,
        IFeatureService featureService)
    {
        _cipherRepository = cipherRepository;
        _folderRepository = folderRepository;
        _organizationRepository = organizationRepository;
        _organizationUserRepository = organizationUserRepository;
        _collectionRepository = collectionRepository;
        _pushService = pushService;
        _policyService = policyService;
        _policyRequirementQuery = policyRequirementQuery;
        _featureService = featureService;
    }

    public async Task ImportIntoIndividualVaultAsync(
        List<Folder> folders,
        List<CipherDetails> ciphers,
        IEnumerable<KeyValuePair<int, int>> folderRelationships,
        Guid importingUserId)
    {
        // Make sure the user can save new ciphers to their personal vault
        var organizationDataOwnershipEnabled = _featureService.IsEnabled(FeatureFlagKeys.PolicyRequirements)
            ? (await _policyRequirementQuery.GetAsync<OrganizationDataOwnershipPolicyRequirement>(importingUserId)).State == OrganizationDataOwnershipState.Enabled
            : await _policyService.AnyPoliciesApplicableToUserAsync(importingUserId, PolicyType.OrganizationDataOwnership);

        if (organizationDataOwnershipEnabled)
        {
            throw new BadRequestException("You cannot import items into your personal vault because you are " +
                "a member of an organization which forbids it.");
        }

        foreach (var cipher in ciphers)
        {
            cipher.SetNewId();

            if (cipher.UserId.HasValue && cipher.Favorite)
            {
                cipher.Favorites = $"{{\"{cipher.UserId.ToString().ToUpperInvariant()}\":\"true\"}}";
            }
        }

        var userfoldersIds = (await _folderRepository.GetManyByUserIdAsync(importingUserId)).Select(f => f.Id).ToList();

        //Assign id to the ones that don't exist in DB
        //Need to keep the list order to create the relationships
        List<Folder> newFolders = new List<Folder>();
        foreach (var folder in folders)
        {
            if (!userfoldersIds.Contains(folder.Id))
            {
                folder.SetNewId();
                newFolders.Add(folder);
            }
        }

        // Create the folder associations based on the newly created folder ids
        foreach (var relationship in folderRelationships)
        {
            var cipher = ciphers.ElementAtOrDefault(relationship.Key);
            var folder = folders.ElementAtOrDefault(relationship.Value);

            if (cipher == null || folder == null)
            {
                continue;
            }

            cipher.Folders = $"{{\"{cipher.UserId.ToString().ToUpperInvariant()}\":" +
                $"\"{folder.Id.ToString().ToUpperInvariant()}\"}}";
        }

        // Create it all
        await _cipherRepository.CreateAsync(importingUserId, ciphers, newFolders);

        // push
        await _pushService.PushSyncVaultAsync(importingUserId);
    }

    public async Task ImportIntoOrganizationalVaultAsync(
        List<Collection> collections,
        List<CipherDetails> ciphers,
        IEnumerable<KeyValuePair<int, int>> collectionRelationships,
        Guid importingUserId)
    {
        var org = collections.Count > 0 ?
            await _organizationRepository.GetByIdAsync(collections[0].OrganizationId) :
            await _organizationRepository.GetByIdAsync(ciphers.FirstOrDefault(c => c.OrganizationId.HasValue).OrganizationId.Value);
        var importingOrgUser = await _organizationUserRepository.GetByOrganizationAsync(org.Id, importingUserId);

        if (collections.Count > 0 && org != null && org.MaxCollections.HasValue)
        {
            var collectionCount = await _collectionRepository.GetCountByOrganizationIdAsync(org.Id);
            if (org.MaxCollections.Value < (collectionCount + collections.Count))
            {
                throw new BadRequestException("This organization can only have a maximum of " +
                    $"{org.MaxCollections.Value} collections.");
            }
        }

        // Init. ids for ciphers
        foreach (var cipher in ciphers)
        {
            cipher.SetNewId();
        }

        var organizationCollectionsIds = (await _collectionRepository.GetManyByOrganizationIdAsync(org.Id)).Select(c => c.Id).ToList();

        //Assign id to the ones that don't exist in DB
        //Need to keep the list order to create the relationships
        var newCollections = new List<Collection>();
        var newCollectionUsers = new List<CollectionUser>();

        foreach (var collection in collections)
        {
            // If the collection already exists, skip it
            if (organizationCollectionsIds.Contains(collection.Id))
            {
                continue;
            }

            // Create new collections if not already present
            collection.SetNewId();
            newCollections.Add(collection);

            /*
             * If the organization was created by a Provider, the organization may have zero members (users)
             * In this situation importingOrgUser will be null, and accessing importingOrgUser.Id will
             * result in a null reference exception.
             *
             * Avoid user assignment, but proceed with adding the collection.
             */
            if (importingOrgUser == null)
            {
                continue;
            }

            newCollectionUsers.Add(new CollectionUser
            {
                CollectionId = collection.Id,
                OrganizationUserId = importingOrgUser.Id,
                Manage = true
            });
        }

        // Create associations based on the newly assigned ids
        var collectionCiphers = new List<CollectionCipher>();
        foreach (var relationship in collectionRelationships)
        {
            var cipher = ciphers.ElementAtOrDefault(relationship.Key);
            var collection = collections.ElementAtOrDefault(relationship.Value);

            if (cipher == null || collection == null)
            {
                continue;
            }

            collectionCiphers.Add(new CollectionCipher
            {
                CipherId = cipher.Id,
                CollectionId = collection.Id
            });
        }

        // Create it all
        await _cipherRepository.CreateAsync(ciphers, newCollections, collectionCiphers, newCollectionUsers);

        // push
        await _pushService.PushSyncVaultAsync(importingUserId);
    }
}
