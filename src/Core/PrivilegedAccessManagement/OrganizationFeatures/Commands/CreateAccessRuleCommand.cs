using Bit.Core.Exceptions;
using Bit.Core.PrivilegedAccessManagement.Entities;
using Bit.Core.PrivilegedAccessManagement.OrganizationFeatures.Commands.Interfaces;
using Bit.Core.PrivilegedAccessManagement.Repositories;
using Bit.Core.PrivilegedAccessManagement.Services;

namespace Bit.Core.PrivilegedAccessManagement.OrganizationFeatures.Commands;

public class CreateAccessRuleCommand : ICreateAccessRuleCommand
{
    private readonly IAccessRuleRepository _repository;
    private readonly IAccessRuleValidator _validator;
    private readonly TimeProvider _timeProvider;

    public CreateAccessRuleCommand(
        IAccessRuleRepository repository,
        IAccessRuleValidator validator,
        TimeProvider timeProvider)
    {
        _repository = repository;
        _validator = validator;
        _timeProvider = timeProvider;
    }

    public async Task<AccessRule> CreateAsync(AccessRule rule)
    {
        if (string.IsNullOrWhiteSpace(rule.Name))
        {
            throw new BadRequestException("Name is required.");
        }

        var validation = _validator.Validate(rule.Rule);
        if (!validation.IsValid)
        {
            throw new BadRequestException(validation.Error!);
        }

        var existing = await _repository.GetManyByOrganizationIdAsync(rule.OrganizationId);
        if (existing.Any(p => string.Equals(p.Name, rule.Name, StringComparison.OrdinalIgnoreCase)))
        {
            throw new BadRequestException("A rule with that name already exists.");
        }

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        rule.CreationDate = now;
        rule.RevisionDate = now;

        return await _repository.CreateAsync(rule);
    }
}
