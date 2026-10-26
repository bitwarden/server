using System.ComponentModel.DataAnnotations;
using Bit.Core.Repositories;
using Bit.Core.Vault.Entities;
using Bit.Core.Vault.Repositories;
using Bit.Seeder.Extensions;
using Bit.Seeder.Services;

namespace Bit.Seeder.Scenes;

/// <summary>
/// Creates an organization-owned login cipher encrypted with the organization's symmetric key and
/// assigns it to the requested collections. Mirrors <see cref="UserLoginCipherScene"/> for org vaults.
/// </summary>
public class OrganizationLoginCipherScene(
    IOrganizationRepository organizationRepository,
    ICipherRepository cipherRepository,
    ICollectionRepository collectionRepository,
    IOrganizationUserRepository organizationUserRepository,
    IFolderRepository folderRepository,
    IManglerService manglerService)
    : LoginCipherScene<OrganizationLoginCipherScene.Request>(manglerService)
{
    public class Request : LoginCipherRequest
    {
        [Required]
        public required Guid OrganizationId { get; set; }
        [Required]
        public required string OrganizationKeyB64 { get; set; }
        [Required]
        public required IEnumerable<Guid> CollectionIds { get; set; }
    }

    protected override async Task<CipherOwner> ResolveOwnerAsync(Request request)
    {
        await organizationRepository.GetDistinctCollectionIdsOrThrowAsync(
            collectionRepository, request.OrganizationId, request.CollectionIds);

        if (request.Favorite || request.Archived || request.FolderId.HasValue)
        {
            await organizationUserRepository.ThrowIfUserNotConfirmedOrRevokedMemberOfOrganizationAsync(
                request.UserId, request.OrganizationId);
        }

        await folderRepository.ThrowIfFolderNotOwnedByUserAsync(request.FolderId, request.UserId);

        return new CipherOwner(request.OrganizationKeyB64, request.OrganizationId, null);
    }

    protected override Task PersistAsync(Cipher cipher, Request request)
        => cipherRepository.CreateAsync(cipher, request.CollectionIds.Distinct());
}
