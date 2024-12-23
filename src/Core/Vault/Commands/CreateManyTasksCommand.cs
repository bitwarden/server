using Bit.Core.Context;
using Bit.Core.Exceptions;
using Bit.Core.Utilities;
using Bit.Core.Vault.Authorization.SecurityTasks;
using Bit.Core.Vault.Commands.Interfaces;
using Bit.Core.Vault.Entities;
using Bit.Core.Vault.Enums;
using Bit.Core.Vault.Models.Api;
using Bit.Core.Vault.Repositories;
using Microsoft.AspNetCore.Authorization;

namespace Bit.Core.Vault.Commands;

public class CreateManyTasksCommand : ICreateManyTasksCommand
{
    private readonly IAuthorizationService _authorizationService;
    private readonly ICurrentContext _currentContext;
    private readonly ISecurityTaskRepository _securityTaskRepository;

    public CreateManyTasksCommand(
        ISecurityTaskRepository securityTaskRepository,
        IAuthorizationService authorizationService,
        ICurrentContext currentContext)
    {
        _securityTaskRepository = securityTaskRepository;
        _authorizationService = authorizationService;
        _currentContext = currentContext;
    }

    /// <inheritdoc />
    public async Task<ICollection<Guid>> CreateAsync(Guid organizationId, IEnumerable<SecurityTaskCreateRequest> tasks)
    {
        if (!_currentContext.UserId.HasValue)
        {
            throw new NotFoundException();
        }

        var tasksList = tasks?.ToList();

        if (tasksList is null || tasksList.Count == 0)
        {
            throw new BadRequestException("No tasks provided.");
        }

        var securityTasks = tasksList.Select(t => new SecurityTask
        {
            OrganizationId = organizationId,
            CipherId = t.CipherId,
            Type = t.Type,
            Status = SecurityTaskStatus.Pending
        }).ToList();

        // Verify authorization for each task
        foreach (var task in securityTasks)
        {
            await _authorizationService.AuthorizeOrThrowAsync(
                _currentContext.HttpContext.User,
                task,
                SecurityTaskOperations.Create);
        }

        return await _securityTaskRepository.CreateManyAsync(securityTasks);
    }
}
