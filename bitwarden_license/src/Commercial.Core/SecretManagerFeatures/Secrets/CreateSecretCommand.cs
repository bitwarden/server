using Bit.Core.Entities;
using Bit.Core.Repositories;
using Bit.Core.SecretManagerFeatures.Secrets.Interfaces;

namespace Bit.Commercial.Core.SecretManagerFeatures.Secrets;

public class CreateSecretCommand : ICreateSecretCommand
{
    private readonly ISecretRepository _secretRepository;
    private readonly IProjectRepository _projectRepository;

    public CreateSecretCommand(ISecretRepository secretRepository, IProjectRepository projectRepository)
    {
        _secretRepository = secretRepository;
        _projectRepository = projectRepository;
    }

    public async Task<Secret> CreateAsync(Secret secret)
    {
        var orgAdmin = await _currentContext.OrganizationAdmin(project.OrganizationId);
        var hasAccess = false;

        if(!secret.projectId){
            hasAccess = orgAdmin;
        } else {
            var accessClient = AccessClientHelper.ToAccessClient(_currentContext.ClientType, orgAdmin);

            hasAccess = accessClient switch
            {
                AccessClientType.NoAccessCheck => true,
                AccessClientType.User => await _projectRepository.UserHasWriteAccessToProject(secret.projectId, userId),
                _ => false,
            };
        }

        if (!hasAccess)
        {
            throw new UnauthorizedAccessException();
        }

        //Check if the project thats associated with the secret gives this user read/write/no access
        return await _secretRepository.CreateAsync(secret);
    }
}
