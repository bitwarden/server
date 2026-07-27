using Bit.Pam.Entities;
using Bit.Pam.Models;
using Bit.Pam.Repositories;
using Bit.Services.Pam.OrganizationFeatures.Commands.Interfaces;
using Bit.Services.Pam.Services;

namespace Bit.Services.Pam.OrganizationFeatures.Commands;

public class CreateAccessRuleCommand : ICreateAccessRuleCommand
{
    private readonly IAccessRuleRepository _repository;
    private readonly IAccessRuleWriteValidator _validator;
    private readonly TimeProvider _timeProvider;

    public CreateAccessRuleCommand(
        IAccessRuleRepository repository,
        IAccessRuleWriteValidator validator,
        TimeProvider timeProvider)
    {
        _repository = repository;
        _validator = validator;
        _timeProvider = timeProvider;
    }

    public async Task<AccessRuleDetails> CreateAsync(AccessRule rule, IEnumerable<Guid> collectionIds)
    {
        var desiredCollectionIds = await _validator.ValidateAsync(rule.OrganizationId, rule, collectionIds);

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        rule.CreationDate = now;
        rule.RevisionDate = now;

        var created = await _repository.CreateAsync(rule);

        await _repository.SetCollectionAssociationsAsync(
            created.OrganizationId, created.Id, desiredCollectionIds, []);

        return AccessRuleDetails.From(created, desiredCollectionIds);
    }
}
