using Bit.Core.Context;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
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
        var secret = await _secretRepository.GetByIdAsync(updatedSecret.Id);
        if (secret == null || !_currentContext.AccessSecretsManager(secret.OrganizationId))
        {
            throw new NotFoundException();
        }

        var orgAdmin = await _currentContext.OrganizationAdmin(secret.OrganizationId);
        var accessClient = AccessClientHelper.ToAccessClient(_currentContext.ClientType, orgAdmin);

        if (!await hasAccessToOriginalAndUpdatedProject(accessClient, secret, updatedSecret, userId))
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

    public async Task<bool> hasAccessToOriginalAndUpdatedProject(AccessClientType accessClient, Secret secret, Secret updatedSecret, Guid userId)
    {
        switch (accessClient) { 
            case AccessClientType.NoAccessCheck: 
                return true; 
            case AccessClientType.User: 
                var oldProject = secret.Projects?.FirstOrDefault(); 
                var newProject = updatedSecret.Projects?.FirstOrDefault(); 
                var accessToOld = oldProject != null && await _projectRepository.UserHasWriteAccessToProject(oldProject.Id, userId);
                var accessToNew = newProject != null && await _projectRepository.UserHasWriteAccessToProject(newProject.Id, userId); 
                return accessToOld && accessToNew; 
            default:
                return false; 
        }
    }
}
