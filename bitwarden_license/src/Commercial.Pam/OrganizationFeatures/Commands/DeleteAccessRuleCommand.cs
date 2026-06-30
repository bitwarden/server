using Bit.Commercial.Pam.OrganizationFeatures.Commands.Interfaces;
using Bit.Core.Exceptions;
using Bit.Pam.Repositories;

namespace Bit.Commercial.Pam.OrganizationFeatures.Commands;

public class DeleteAccessRuleCommand : IDeleteAccessRuleCommand
{
    private readonly IAccessRuleRepository _repository;
    private readonly TimeProvider _timeProvider;

    public DeleteAccessRuleCommand(IAccessRuleRepository repository, TimeProvider timeProvider)
    {
        _repository = repository;
        _timeProvider = timeProvider;
    }

    public async Task DeleteAsync(Guid organizationId, Guid id, Guid? deletedBy)
    {
        var existing = await _repository.GetByIdAsync(id);
        if (existing is null || existing.OrganizationId != organizationId)
        {
            throw new NotFoundException();
        }

        // Soft-delete: the rule row and its collection links survive (for the audit trail), but every gating read
        // excludes it, so it stops governing. GetByIdAsync already filters deleted rules, so a repeat delete is a 404.
        await _repository.SoftDeleteAsync(id, deletedBy, _timeProvider.GetUtcNow().UtcDateTime);
    }
}
