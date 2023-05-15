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

    public CreateProjectCommand(
        IAccessPolicyRepository accessPolicyRepository,
        IOrganizationUserRepository organizationUserRepository,
        IProjectRepository projectRepository)
    {
        _accessPolicyRepository = accessPolicyRepository;
        _organizationUserRepository = organizationUserRepository;
        _projectRepository = projectRepository;
    }

    public async Task<Project> CreateAsync(Project project, Guid userId)
    {
        var createdProject = await _projectRepository.CreateAsync(project);

        var orgUser = await _organizationUserRepository.GetByOrganizationAsync(createdProject.OrganizationId,
            userId);
        var accessPolicy = new UserProjectAccessPolicy()
        {
            OrganizationUserId = orgUser.Id,
            GrantedProjectId = createdProject.Id,
            Read = true,
            Write = true,
        };
        await _accessPolicyRepository.CreateManyAsync(new List<BaseAccessPolicy> { accessPolicy });
        return createdProject;
    }
}
