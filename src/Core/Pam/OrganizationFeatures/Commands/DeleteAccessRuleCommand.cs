using Bit.Core.Exceptions;
using Bit.Core.Pam.OrganizationFeatures.Commands.Interfaces;
using Bit.Core.Pam.Repositories;

namespace Bit.Core.Pam.OrganizationFeatures.Commands;

public class DeleteAccessRuleCommand : IDeleteAccessRuleCommand
{
    private readonly IAccessRuleRepository _repository;

    public DeleteAccessRuleCommand(IAccessRuleRepository repository)
    {
        _repository = repository;
    }

    public async Task DeleteAsync(Guid organizationId, Guid id)
    {
        var existing = await _repository.GetByIdAsync(id);
        if (existing is null || existing.OrganizationId != organizationId)
        {
            throw new NotFoundException();
        }

        await _repository.DeleteAsync(existing);
    }
}
