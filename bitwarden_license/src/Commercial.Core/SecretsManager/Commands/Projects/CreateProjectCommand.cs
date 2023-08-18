using Bit.Core.Context;
using Bit.Core.Exceptions;
using Bit.Core.Identity;
using Bit.Core.Repositories;
using Bit.Core.SecretsManager.Commands.Projects.Interfaces;
using Bit.Core.SecretsManager.Entities;
using Bit.Core.SecretsManager.Repositories;

namespace Bit.Commercial.Core.SecretsManager.Commands.Projects;

public class CreateProjectCommand : ICreateProjectCommand
{
    private readonly IAccessPolicyRepository _accessPolicyRepository;
    private readonly IOrganizationUserRepository _organizationUserRepository;
    private readonly IProjectRepository _projectRepository;
    private readonly ICurrentContext _currentContext;


    public CreateProjectCommand(
        IAccessPolicyRepository accessPolicyRepository,
        IOrganizationUserRepository organizationUserRepository,
        IProjectRepository projectRepository,
        ICurrentContext currentContext)
    {
        _accessPolicyRepository = accessPolicyRepository;
        _organizationUserRepository = organizationUserRepository;
        _projectRepository = projectRepository;
        _currentContext = currentContext;
    }

    public async Task<Project> CreateAsync(Project project, Guid id, ClientType clientType)
    {
        if (clientType != ClientType.User && clientType != ClientType.ServiceAccount)
        {
            throw new NotFoundException();
        }

        var createdProject = await _projectRepository.CreateAsync(project);

        if (clientType == ClientType.User)
        {
            var orgUser = await _organizationUserRepository.GetByOrganizationAsync(createdProject.OrganizationId, id);

            var accessPolicy = new UserProjectAccessPolicy()
            {
                OrganizationUserId = orgUser.Id,
                GrantedProjectId = createdProject.Id,
                Read = true,
                Write = true,
            };

            await _accessPolicyRepository.CreateManyAsync(new List<BaseAccessPolicy> { accessPolicy });

        }
        else if (clientType == ClientType.ServiceAccount)
        {
            var serviceAccountProjectAccessPolicy = new ServiceAccountProjectAccessPolicy()
            {
                ServiceAccountId = id,
                GrantedProjectId = createdProject.Id,
                Read = true,
                Write = true,
            };

            await _accessPolicyRepository.CreateManyAsync(new List<BaseAccessPolicy> { serviceAccountProjectAccessPolicy });
        }

        return createdProject;
    }
}
