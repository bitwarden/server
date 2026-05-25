using Bit.Core.Exceptions;
using Bit.Core.PrivilegedAccessManagement.Entities;
using Bit.Core.PrivilegedAccessManagement.OrganizationFeatures.Commands.Interfaces;
using Bit.Core.PrivilegedAccessManagement.Repositories;
using Bit.Core.PrivilegedAccessManagement.Services;

namespace Bit.Core.PrivilegedAccessManagement.OrganizationFeatures.Commands;

public class CreateLeasingPolicyCommand : ICreateLeasingPolicyCommand
{
    private readonly ILeasingPolicyRepository _repository;
    private readonly ILeasingPolicyValidator _validator;
    private readonly TimeProvider _timeProvider;

    public CreateLeasingPolicyCommand(
        ILeasingPolicyRepository repository,
        ILeasingPolicyValidator validator,
        TimeProvider timeProvider)
    {
        _repository = repository;
        _validator = validator;
        _timeProvider = timeProvider;
    }

    public async Task<LeasingPolicy> CreateAsync(LeasingPolicy policy)
    {
        if (string.IsNullOrWhiteSpace(policy.Name))
        {
            throw new BadRequestException("Name is required.");
        }

        var validation = _validator.Validate(policy.Policy);
        if (!validation.IsValid)
        {
            throw new BadRequestException(validation.Error!);
        }

        var existing = await _repository.GetManyByOrganizationIdAsync(policy.OrganizationId);
        if (existing.Any(p => string.Equals(p.Name, policy.Name, StringComparison.OrdinalIgnoreCase)))
        {
            throw new BadRequestException("A policy with that name already exists.");
        }

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        policy.CreationDate = now;
        policy.RevisionDate = now;

        return await _repository.CreateAsync(policy);
    }
}
