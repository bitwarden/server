using Bit.Core.Exceptions;
using Bit.Pam.Repositories;
using Bit.Services.Pam.OrganizationFeatures.Commands.Interfaces;

namespace Bit.Services.Pam.OrganizationFeatures.Commands;

public class DeleteAccessRuleCommand : IDeleteAccessRuleCommand
{
    private readonly IAccessRuleRepository _repository;

    public DeleteAccessRuleCommand(IAccessRuleRepository repository)
    {
        _repository = repository;
    }

    public async Task DeleteAsync(Guid organizationId, Guid id, Guid? deletedBy)
    {
        var existing = await _repository.GetByIdAsync(id);
        if (existing is null || existing.OrganizationId != organizationId)
        {
            throw new NotFoundException();
        }

        // Hard delete: remove the rule and clear its collection links (they become ungoverned).
        await _repository.DeleteAsync(existing);
    }
}
