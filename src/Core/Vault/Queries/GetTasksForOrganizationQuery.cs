﻿using Bit.Core.Context;
using Bit.Core.Exceptions;
using Bit.Core.Utilities;
using Bit.Core.Vault.Authorization.SecurityTasks;
using Bit.Core.Vault.Entities;
using Bit.Core.Vault.Enums;
using Bit.Core.Vault.Repositories;
using Microsoft.AspNetCore.Authorization;

namespace Bit.Core.Vault.Queries;

public class GetTasksForOrganizationQuery : IGetTasksForOrganizationQuery
{
    private readonly ISecurityTaskRepository _securityTaskRepository;
    private readonly IAuthorizationService _authorizationService;
    private readonly ICurrentContext _currentContext;

    public GetTasksForOrganizationQuery(
        ISecurityTaskRepository securityTaskRepository,
        IAuthorizationService authorizationService,
        ICurrentContext currentContext
    )
    {
        _securityTaskRepository = securityTaskRepository;
        _authorizationService = authorizationService;
        _currentContext = currentContext;
    }

    public async Task<ICollection<SecurityTask>> GetTasksAsync(Guid organizationId,
        SecurityTaskStatus? status = null)
    {
        var organization = _currentContext.GetOrganization(organizationId);
        var userId = _currentContext.UserId;

        if (organization == null || !userId.HasValue)
        {
            throw new NotFoundException();
        }

        await _authorizationService.AuthorizeOrThrowAsync(_currentContext.HttpContext.User, organization, SecurityTaskOperations.ListAllForOrganization);

        return (await _securityTaskRepository.GetManyByOrganizationIdStatusAsync(organizationId, status)).ToList();
    }
}
