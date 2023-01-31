using Bit.Core.Context;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.SecretsManager.Commands.Secrets.Interfaces;
using Bit.Core.SecretsManager.Entities;
using Bit.Core.SecretsManager.Repositories;

namespace Bit.Commercial.Core.SecretsManager.Commands.Secrets;

public class DeleteSecretCommand : IDeleteSecretCommand
{
    private readonly ICurrentContext _currentContext;
    private readonly ISecretRepository _secretRepository;
    private readonly IProjectRepository _projectRepository;

    public DeleteSecretCommand(ISecretRepository secretRepository, IProjectRepository projectRepository, ICurrentContext currentContext)
    {
        _currentContext = currentContext;
        _secretRepository = secretRepository;
        _projectRepository = projectRepository;
        _currentContext = currentContext;
    }

    public async Task<List<Tuple<Secret, string>>> DeleteSecrets(List<Guid> ids, Guid userId, Guid organizationId)
    {
        var orgAdmin = await _currentContext.OrganizationAdmin(organizationId);
        var accessClient = AccessClientHelper.ToAccessClient(_currentContext.ClientType, orgAdmin);
        var secrets = await _secretRepository.GetManyByIds(ids);

        if (secrets?.Any() != true)
        {
            throw new NotFoundException();
        }
        
        // Ensure all secrets belongs to the same organization
        if (secrets.Any(p => p.OrganizationId != organizationId))
        {
            throw new BadRequestException();
        }

        if (!_currentContext.AccessSecretsManager(organizationId))
        {
            throw new NotFoundException();
        }

        var results = new List<Tuple<Secret, string>>();
        var deleteIds = new List<Guid>(); 

        foreach(var id in ids)
        {
            var secret = secrets.FirstOrDefault(secret => secret.Id == id);


            if (secret == null)
            {
                throw new NotFoundException();
            }
            else
            {
                var hasAccess = orgAdmin;

                if (secret.Projects != null && secret.Projects?.Count > 0)
                {
                    var projectId = secret.Projects.FirstOrDefault().Id;

                    hasAccess = accessClient switch
                    {
                        AccessClientType.NoAccessCheck => true,
                        AccessClientType.User => await _projectRepository.UserHasWriteAccessToProject(projectId, userId),
                        _ => false,
                    };
                }

                if (!hasAccess)
                {
                    throw new NotFoundException();
                }

                deleteIds.Add(id);
                results.Add(new Tuple<Secret, string>(secret, ""));
            }
        };

        if(deleteIds.Count > 0){
            await _secretRepository.SoftDeleteManyByIdAsync(deleteIds);
        }
       
        return results;
    }
}

