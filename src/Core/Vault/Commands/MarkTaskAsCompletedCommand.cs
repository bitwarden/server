using Bit.Core.Context;
using Bit.Core.Exceptions;
using Bit.Core.Utilities;
using Bit.Core.Vault.Authorization;
using Bit.Core.Vault.Commands.Interfaces;
using Bit.Core.Vault.Enums;
using Bit.Core.Vault.Repositories;
using Microsoft.AspNetCore.Authorization;

namespace Bit.Core.Vault.Commands;

public class MarkTaskAsCompletedCommand : IMarkTaskAsCompleteCommand
{
    private readonly ISecurityTaskRepository _securityTaskRepository;
    private readonly IAuthorizationService _authorizationService;
    private readonly ICurrentContext _currentContext;

    public MarkTaskAsCompletedCommand(
        ISecurityTaskRepository securityTaskRepository,
        IAuthorizationService authorizationService,
        ICurrentContext currentContext)
    {
        _securityTaskRepository = securityTaskRepository;
        _authorizationService = authorizationService;
        _currentContext = currentContext;
    }

    /// <inheritdoc />
    public async Task CompleteAsync(Guid taskId)
    {
        if (!_currentContext.UserId.HasValue)
        {
            throw new NotFoundException();
        }

        var task = await _securityTaskRepository.GetByIdAsync(taskId);
        if (task is null)
        {
            throw new NotFoundException();
        }

        await _authorizationService.AuthorizeOrThrowAsync(_currentContext.HttpContext.User, task,
            SecurityTaskOperations.Update);

        task.Status = SecurityTaskStatus.Completed;
        task.RevisionDate = DateTime.UtcNow;

        await _securityTaskRepository.ReplaceAsync(task);
    }
}
