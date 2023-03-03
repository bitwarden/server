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
        var originalProject = secret.Projects?.FirstOrDefault();

        var hasAccessToOriginalProject = accessClient switch
        {
            AccessClientType.NoAccessCheck => true,
            AccessClientType.User => originalProject != null && await _projectRepository.UserHasWriteAccessToProject(originalProject.Id, userId),
            _ => false,
        };

        var newlyAssignedProject = updatedSecret.Projects?.FirstOrDefault();
        var hasAccessToNewlyAssignedProject = hasAccessToOriginalProject;

        if(newlyAssignedProject.Id != originalProject.Id)
        {
            hasAccessToNewlyAssignedProject = accessClient switch
            {
                AccessClientType.NoAccessCheck => true,
                AccessClientType.User => newlyAssignedProject != null && await _projectRepository.UserHasWriteAccessToProject(newlyAssignedProject.Id, userId),
                _ => false,
            };
        }

        if (!hasAccessToOriginalProject || !hasAccessToNewlyAssignedProject)
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
