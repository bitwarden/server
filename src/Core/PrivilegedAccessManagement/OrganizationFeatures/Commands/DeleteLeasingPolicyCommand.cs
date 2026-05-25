using Bit.Core.Exceptions;
using Bit.Core.PrivilegedAccessManagement.OrganizationFeatures.Commands.Interfaces;
using Bit.Core.PrivilegedAccessManagement.Repositories;

namespace Bit.Core.PrivilegedAccessManagement.OrganizationFeatures.Commands;

public class DeleteLeasingPolicyCommand : IDeleteLeasingPolicyCommand
{
    private readonly ILeasingPolicyRepository _repository;

    public DeleteLeasingPolicyCommand(ILeasingPolicyRepository repository)
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
