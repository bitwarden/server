using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Pam.Entities;
using Bit.Pam.Models;
using Bit.Pam.Repositories;
using Bit.Services.Pam.OrganizationFeatures.Commands.Interfaces;
using Bit.Services.Pam.Services;

namespace Bit.Services.Pam.OrganizationFeatures.Commands;

public class UpdateAccessRuleCommand : IUpdateAccessRuleCommand
{
    private readonly IAccessRuleRepository _repository;
    private readonly ICollectionRepository _collectionRepository;
    private readonly IAccessRuleWriteValidator _validator;
    private readonly TimeProvider _timeProvider;

    public UpdateAccessRuleCommand(
        IAccessRuleRepository repository,
        ICollectionRepository collectionRepository,
        IAccessRuleWriteValidator validator,
        TimeProvider timeProvider)
    {
        _repository = repository;
        _collectionRepository = collectionRepository;
        _validator = validator;
        _timeProvider = timeProvider;
    }

    public async Task<AccessRuleDetails> UpdateAsync(Guid organizationId, Guid id, AccessRule update,
        IEnumerable<Guid> collectionIds)
    {
        var existing = await _repository.GetDetailsByIdAsync(id);
        if (existing is null || existing.OrganizationId != organizationId)
        {
            throw new NotFoundException();
        }

        var desiredCollectionIds = await _validator.ValidateAsync(organizationId, update, collectionIds, id);

        // Persist a plain AccessRule: the AccessRuleDetails returned by GetDetailsByIdAsync carries an extra
        // CollectionIds property that the base ReplaceAsync would otherwise forward to AccessRule_Update.
        var toPersist = new AccessRule
        {
            Id = existing.Id,
            OrganizationId = existing.OrganizationId,
            Name = update.Name,
            Description = update.Description,
            Conditions = update.Conditions,
            SingleActiveLease = update.SingleActiveLease,
            DefaultLeaseDurationSeconds = update.DefaultLeaseDurationSeconds,
            MaxLeaseDurationSeconds = update.MaxLeaseDurationSeconds,
            Enabled = update.Enabled,
            AllowsExtensions = update.AllowsExtensions,
            MaxExtensionDurationSeconds = update.MaxExtensionDurationSeconds,
            CreationDate = existing.CreationDate,
            RevisionDate = _timeProvider.GetUtcNow().UtcDateTime,
            LastEditedBy = update.LastEditedBy,
        };

        await _repository.ReplaceAsync(toPersist);

        var toClear = existing.CollectionIds.Except(desiredCollectionIds).ToList();
        await _collectionRepository.SetAccessRuleAssociationsAsync(organizationId, id, desiredCollectionIds, toClear);

        return AccessRuleDetails.From(toPersist, desiredCollectionIds);
    }
}
