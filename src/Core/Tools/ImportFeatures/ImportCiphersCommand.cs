using Bit.Core.AdminConsole.OrganizationFeatures.Policies;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyRequirements;
using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Exceptions;
using Bit.Core.Platform.Push;
using Bit.Core.Repositories;
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
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IOrganizationUserRepository _organizationUserRepository;
    private readonly ICollectionRepository _collectionRepository;
    private readonly IPolicyRequirementQuery _policyRequirementQuery;
    private readonly ICurrentContext _currentContext;

    public ImportCiphersCommand(
        ICipherRepository cipherRepository,
        IFolderRepository folderRepository,
        ICollectionRepository collectionRepository,
        IOrganizationRepository organizationRepository,
        IOrganizationUserRepository organizationUserRepository,
        IPushNotificationService pushService,
        IPolicyRequirementQuery policyRequirementQuery,
        ICurrentContext currentContext)
    {
        _cipherRepository = cipherRepository;
        _folderRepository = folderRepository;
        _organizationRepository = organizationRepository;
        _organizationUserRepository = organizationUserRepository;
        _collectionRepository = collectionRepository;
        _pushService = pushService;
        _policyRequirementQuery = policyRequirementQuery;
        _currentContext = currentContext;
    }

    public async Task ImportIntoIndividualVaultAsync(
        List<Folder> folders,
        List<CipherDetails> ciphers,
        IEnumerable<KeyValuePair<int, int>> folderRelationships,
        Guid importingUserId)
    {
        // Make sure the user can save new ciphers to their personal vault
        var organizationDataOwnershipPolicyRequirement =
            await _policyRequirementQuery.GetAsync<OrganizationDataOwnershipPolicyRequirement>(importingUserId);

        if (organizationDataOwnershipPolicyRequirement.State == OrganizationDataOwnershipState.Enabled)
        {
            throw new BadRequestException("You cannot import items into your personal vault because you are " +
                "a member of an organization which forbids it.");
        }

        foreach (var cipher in ciphers)
        {
            cipher.SetNewId();

            if (cipher.UserId.HasValue && cipher.Favorite)
            {
                cipher.Favorites = $"{{\"{cipher.UserId.ToString()!.ToUpperInvariant()}\":true}}";
            }

            if (cipher.UserId.HasValue && cipher.ArchivedDate.HasValue)
            {
                cipher.Archives = $"{{\"{cipher.UserId.Value.ToString().ToUpperInvariant()}\":\"" +
                                  $"{cipher.ArchivedDate.Value:yyyy-MM-ddTHH:mm:ss.fffffffZ}\"}}";
            }
        }

        var userfoldersIds = (await _folderRepository.GetManyByUserIdAsync(importingUserId)).Select(f => f.Id).ToList();

        //Assign id to the ones that don't exist in DB
        //Need to keep the list order to create the relationships
        var newFolders = new List<Folder>();
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

            cipher.Folders = $"{{\"{cipher.UserId.ToString()!.ToUpperInvariant()}\":" +
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
        var orgId = collections.Count > 0
            ? collections[0].OrganizationId
            : ciphers.FirstOrDefault(c => c.OrganizationId.HasValue)?.OrganizationId;

        if (orgId is null)
        {
            throw new BadRequestException("No organization ID found in the import data.");
        }

        var org = await _organizationRepository.GetByIdAsync(orgId.Value);
        if (org is null)
        {
            throw new NotFoundException("Organization not found.");
        }

        var importingOrgUser = await _organizationUserRepository.GetByOrganizationAsync(org.Id, importingUserId);
        // A managed service provider is expected to be able to perform imports on behalf of a managed org
        // In this situation importingOrgUser will be null, cross-check MSP status
        if (importingOrgUser is null && !await _currentContext.ProviderUserForOrgAsync(org.Id))
        {
            throw new UnauthorizedAccessException(
                "An organization import can only be performed by organization members or authorized providers");
        }

        if (collections.Count > 0 && org.MaxCollections.HasValue)
        {
            var collectionCount = await _collectionRepository.GetCountByOrganizationIdAsync(org.Id);
            if (org.MaxCollections.Value < (collectionCount + collections.Count))
            {
                throw new BadRequestException("This organization can only have a maximum of " +
                    $"{org.MaxCollections.Value} collections.");
            }
        }

        foreach (var cipher in ciphers)
        {
            // Init. ids for ciphers
            cipher.SetNewId();

            if (cipher.ArchivedDate.HasValue)
            {
                cipher.Archives = $"{{\"{importingUserId.ToString().ToUpperInvariant()}\":\"" +
                                  $"{cipher.ArchivedDate.Value:yyyy-MM-ddTHH:mm:ss.fffffffZ}\"}}";
            }
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
