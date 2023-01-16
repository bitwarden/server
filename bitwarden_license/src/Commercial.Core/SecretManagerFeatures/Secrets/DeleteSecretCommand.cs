using Bit.Core.Entities;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Core.SecretManagerFeatures.Secrets.Interfaces;

namespace Bit.Commercial.Core.SecretManagerFeatures.Secrets;

public class DeleteSecretCommand : IDeleteSecretCommand
{
    private readonly ISecretRepository _secretRepository;
    private readonly IProjectRepository _projectRepository;

    public DeleteSecretCommand(ISecretRepository secretRepository, IProjectRepository projectRepository)
    {
        _secretRepository = secretRepository;
        _projectRepository = projectRepository;
    }

    public async Task<List<Tuple<Secret, string>>> DeleteSecrets(List<Guid> ids)
    {
        var secrets = await _secretRepository.GetManyByIds(ids);

        if (secrets?.Any() != true)
        {
            throw new NotFoundException();
        }

        var results = ids.Select(id =>
        {
            var secret = secrets.FirstOrDefault(secret => secret.Id == id);
            if (secret == null)
            {
                throw new NotFoundException();
            }
            else
            {
                //Check if the Project this secret is associated with allows deletion (write permisison)
                //Check if this secret has a projId
                var orgAdmin = await _currentContext.OrganizationAdmin(secret.OrganizationId);
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

                return new Tuple<Secret, string>(secret, "");
            }
        }).ToList();

        await _secretRepository.SoftDeleteManyByIdAsync(ids);
        return results;
    }
}

