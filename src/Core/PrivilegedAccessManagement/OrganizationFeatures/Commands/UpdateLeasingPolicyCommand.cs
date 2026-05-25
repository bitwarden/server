using Bit.Core.Exceptions;
using Bit.Core.PrivilegedAccessManagement.Entities;
using Bit.Core.PrivilegedAccessManagement.OrganizationFeatures.Commands.Interfaces;
using Bit.Core.PrivilegedAccessManagement.Repositories;
using Bit.Core.PrivilegedAccessManagement.Services;

namespace Bit.Core.PrivilegedAccessManagement.OrganizationFeatures.Commands;

public class UpdateLeasingPolicyCommand : IUpdateLeasingPolicyCommand
{
    private readonly ILeasingPolicyRepository _repository;
    private readonly ILeasingPolicyValidator _validator;
    private readonly TimeProvider _timeProvider;

    public UpdateLeasingPolicyCommand(
        ILeasingPolicyRepository repository,
        ILeasingPolicyValidator validator,
        TimeProvider timeProvider)
    {
        _repository = repository;
        _validator = validator;
        _timeProvider = timeProvider;
    }

    public async Task<LeasingPolicy> UpdateAsync(Guid organizationId, Guid id, LeasingPolicy update)
    {
        if (string.IsNullOrWhiteSpace(update.Name))
        {
            throw new BadRequestException("Name is required.");
        }

        var existing = await _repository.GetByIdAsync(id);
        if (existing is null || existing.OrganizationId != organizationId)
        {
            throw new NotFoundException();
        }

        var validation = _validator.Validate(update.Policy);
        if (!validation.IsValid)
        {
            throw new BadRequestException(validation.Error!);
        }

        var siblings = await _repository.GetManyByOrganizationIdAsync(organizationId);
        if (siblings.Any(p => p.Id != id && string.Equals(p.Name, update.Name, StringComparison.OrdinalIgnoreCase)))
        {
            throw new BadRequestException("A policy with that name already exists.");
        }

        existing.Name = update.Name;
        existing.Description = update.Description;
        existing.Policy = update.Policy;
        existing.RevisionDate = _timeProvider.GetUtcNow().UtcDateTime;

        await _repository.ReplaceAsync(existing);
        return existing;
    }
}
