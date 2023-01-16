using Bit.Core.Entities;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Core.SecretManagerFeatures.Secrets.Interfaces;

namespace Bit.Commercial.Core.SecretManagerFeatures.Secrets;

public class UpdateSecretCommand : IUpdateSecretCommand
{
    private readonly ISecretRepository _secretRepository;
    private readonly IProjectRepository _projectRepository;

    public UpdateSecretCommand(ISecretRepository secretRepository, IProjectRepository projectRepository)
    {
        _secretRepository = secretRepository;
        _projectRepository = projectRepository;
    }

    public async Task<Secret> UpdateAsync(Secret updatedSecret)
    {
        var orgAdmin = await _currentContext.OrganizationAdmin(updatedSecret.OrganizationId);
        var hasAccess = false;

        if(!secret.projectId){
            hasAccess = orgAdmin;
        } else {
            var accessClient = AccessClientHelper.ToAccessClient(_currentContext.ClientType, orgAdmin);

            hasAccess = accessClient switch
            {
                AccessClientType.NoAccessCheck => true,
                AccessClientType.User => await _projectRepository.UserHasWriteAccessToProject(updatedSecret.projectId, userId),
                _ => false,
            };
        }

        if (!hasAccess)
        {
            throw new UnauthorizedAccessException();
        }

        var existingSecret = await _secretRepository.GetByIdAsync(secret.Id);
        if (existingSecret == null)
        {
            throw new NotFoundException();
        }

        secret.OrganizationId = existingSecret.OrganizationId;
        secret.CreationDate = existingSecret.CreationDate;
        secret.DeletedDate = existingSecret.DeletedDate;
        secret.RevisionDate = DateTime.UtcNow;

        await _secretRepository.UpdateAsync(secret);
        return secret;
    }
}
