using Bit.Core.Entities;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Core.SecretManagerFeatures.Secrets.Interfaces;
using Bit.Core.Enums;
using Bit.Core.Context;

namespace Bit.Commercial.Core.SecretManagerFeatures.Secrets;

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
        var hasAccess = false;

        var project = updatedSecret.Projects?.FirstOrDefault();
        if(project == null){
            hasAccess = orgAdmin;
        } else {

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

        var existingSecret = await _secretRepository.GetByIdAsync(updatedSecret.Id, userId, accessClient, orgAdmin);
        if (existingSecret == null)
        {
            throw new NotFoundException();
        }

        updatedSecret.OrganizationId = existingSecret.OrganizationId;
        updatedSecret.CreationDate = existingSecret.CreationDate;
        updatedSecret.DeletedDate = existingSecret.DeletedDate;
        updatedSecret.RevisionDate = DateTime.UtcNow;

        await _secretRepository.UpdateAsync(updatedSecret, userId, accessClient, orgAdmin);
        return updatedSecret;
    }
}
