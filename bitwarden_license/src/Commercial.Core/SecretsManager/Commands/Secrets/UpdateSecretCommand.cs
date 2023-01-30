using Bit.Core.Exceptions;
using Bit.Core.Enums;
using Bit.Core.Context;
﻿using Bit.Core.Exceptions;
using Bit.Core.SecretsManager.Commands.Secrets.Interfaces;
using Bit.Core.SecretsManager.Entities;
using Bit.Core.SecretsManager.Repositories;

namespace Bit.Commercial.Core.SecretsManager.Commands.Secrets;

public class UpdateSecretCommand : IUpdateSecretCommand
{
    private readonly ISecretRepository _secretRepository;
    private readonly IProjectRepository _projectRepository;
    private readonly ICurrentContext _currentContext;

    public UpdateSecretCommand(ISecretRepository secretRepository, IProjectRepository projectRepository, ICurrentContext currentContext)
    {
        _secretRepository = secretRepository;
        _projectRepository = projectRepository;
        _currentContext = currentContext;
    }

    public async Task<Secret> UpdateAsync(Secret updatedSecret, Guid userId)
    {
        var orgAdmin = await _currentContext.OrganizationAdmin(updatedSecret.OrganizationId);
        var accessClient = AccessClientHelper.ToAccessClient(_currentContext.ClientType, orgAdmin);
        var hasAccess = orgAdmin;

        var project = updatedSecret.Projects?.FirstOrDefault();
        if(project != null){
            hasAccess = accessClient switch
            {
                AccessClientType.NoAccessCheck => true,
                AccessClientType.User => await _projectRepository.UserHasWriteAccessToProject(project.Id, userId),
                _ => false,
            };
        }

        if (!hasAccess)
        {
            throw new UnauthorizedAccessException();
        }

        var secret = await _secretRepository.GetByIdAsync(updatedSecret.Id);
        if (secret == null)
        {
            throw new NotFoundException();
        }
        
        secret.Key = updatedSecret.Key;
        secret.Value = updatedSecret.Value;
        secret.Note = updatedSecret.Note;
        secret.Projects = updatedSecret.Projects;
        secret.RevisionDate = DateTime.UtcNow;

        await _secretRepository.UpdateAsync(secret);
        return secret;
    }
}
