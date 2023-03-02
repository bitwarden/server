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
    }

    public async Task<List<Tuple<Secret, string>>> DeleteSecrets(List<Guid> ids, Guid userId)
    {
        var secrets = (await _secretRepository.GetManyByIds(ids)).ToList();

        if (secrets.Any() != true)
        {
            throw new NotFoundException();
        }

        // Ensure all secrets belongs to the same organization
        var organizationId = secrets.First().OrganizationId;
        if (secrets.Any(secret => secret.OrganizationId != organizationId))
        {
            throw new BadRequestException();
        }

        if (!_currentContext.AccessSecretsManager(organizationId))
        {
            throw new NotFoundException();
        }

        var orgAdmin = await _currentContext.OrganizationAdmin(organizationId);
        var accessClient = AccessClientHelper.ToAccessClient(_currentContext.ClientType, orgAdmin);

        var results = new List<Tuple<Secret, string>>();
        var deleteIds = new List<Guid>();

        foreach (var secret in secrets)
        {
            var hasAccess = orgAdmin;

            if (secret.Projects != null && secret.Projects?.Count > 0)
            {
                var projectId = secret.Projects.First().Id;

                hasAccess = accessClient switch
                {
                    AccessClientType.NoAccessCheck => true,
                    AccessClientType.User => await _projectRepository.UserHasWriteAccessToProject(projectId, userId),
                    _ => false,
                };
            }

            if (!hasAccess)
            {
                results.Add(new Tuple<Secret, string>(secret, "access denied"));
            }
            else
            {
                deleteIds.Add(secret.Id);
                results.Add(new Tuple<Secret, string>(secret, ""));
            }
        }



        if (deleteIds.Count > 0)
        {
            await _secretRepository.SoftDeleteManyByIdAsync(deleteIds);
        }

        return results;
    }
}

