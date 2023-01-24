using Bit.Core.Entities;
using Bit.Core.Repositories;
using Bit.Core.SecretManagerFeatures.Secrets.Interfaces;
using Bit.Core.Enums;
using Bit.Core.Context;

namespace Bit.Commercial.Core.SecretManagerFeatures.Secrets;

public class CreateSecretCommand : ICreateSecretCommand
{
    private readonly ISecretRepository _secretRepository;
    private readonly IProjectRepository _projectRepository;
    private readonly ICurrentContext _currentContext;

    public CreateSecretCommand(ISecretRepository secretRepository, IProjectRepository projectRepository, ICurrentContext currentContext)
    {
        _secretRepository = secretRepository;
        _projectRepository = projectRepository;
        _currentContext = currentContext;
    }

    public async Task<Secret> CreateAsync(Secret secret, Guid userId)
    {
        var orgAdmin = await _currentContext.OrganizationAdmin(secret.OrganizationId);
        var accessClient = AccessClientHelper.ToAccessClient(_currentContext.ClientType, orgAdmin);

        var hasAccess = false;

        var project = secret.Projects?.FirstOrDefault();
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

        return await _secretRepository.CreateAsync(secret);
    }
}
