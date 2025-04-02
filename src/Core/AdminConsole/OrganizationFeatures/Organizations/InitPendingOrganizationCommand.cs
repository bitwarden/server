using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Models.Commands;
using Bit.Core.Models.Data;
using Bit.Core.Repositories;
using Bit.Core.Services;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers;

public class InitPendingOrganizationCommand : IInitPendingOrganizationCommand
{

    private readonly IOrganizationService _organizationService;
    private readonly ICollectionRepository _collectionRepository;
    private readonly IOrganizationRepository _organizationRepository;

    public InitPendingOrganizationCommand(
            IOrganizationService organizationService,
            ICollectionRepository collectionRepository,
            IOrganizationRepository organizationRepository
            )
    {
        _organizationService = organizationService;
        _collectionRepository = collectionRepository;
        _organizationRepository = organizationRepository;
    }

    public const string OrgEnabled = "Organization is already enabled.";
    public const string OrgNotPending = "Organization is not on a Pending status.";
    public const string OrgHasPublicKey = "Organization already has a Public Key.";
    public const string OrgHasPrivateKey = "Organization already has a Private Key.";

    public async Task<CommandResult> InitPendingOrganizationAsync(Guid userId, Guid organizationId, Guid organizationUserId, string publicKey, string privateKey, string collectionName)
    {
        await _organizationService.ValidateSignUpPoliciesAsync(userId);

        var org = await _organizationRepository.GetByIdAsync(organizationId);

        if (org.Enabled)
        {
            return new CommandResult(OrgEnabled);
        }

        if (org.Status != OrganizationStatusType.Pending)
        {
            return new CommandResult(OrgNotPending);
        }

        if (!string.IsNullOrEmpty(org.PublicKey))
        {
            return new CommandResult(OrgHasPublicKey);
        }

        if (!string.IsNullOrEmpty(org.PrivateKey))
        {
            return new CommandResult(OrgHasPrivateKey);
        }

        org.Enabled = true;
        org.Status = OrganizationStatusType.Created;
        org.PublicKey = publicKey;
        org.PrivateKey = privateKey;

        await _organizationService.UpdateAsync(org);

        if (!string.IsNullOrWhiteSpace(collectionName))
        {
            // give the owner Can Manage access over the default collection
            List<CollectionAccessSelection> defaultOwnerAccess =
                [new CollectionAccessSelection { Id = organizationUserId, HidePasswords = false, ReadOnly = false, Manage = true }];

            var defaultCollection = new Collection
            {
                Name = collectionName,
                OrganizationId = org.Id
            };
            await _collectionRepository.CreateAsync(defaultCollection, null, defaultOwnerAccess);
        }

        return new CommandResult();
    }
}
