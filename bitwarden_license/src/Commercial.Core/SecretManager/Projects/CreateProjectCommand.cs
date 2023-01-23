using Bit.Core.SecretsManager.Commands.Projects.Interfaces;
using Bit.Core.SecretsManager.Entities;
using Bit.Core.SecretsManager.Repositories;

namespace Bit.Commercial.Core.SecretManager.Projects;

public class CreateProjectCommand : ICreateProjectCommand
{
    private readonly IProjectRepository _projectRepository;

    public CreateProjectCommand(IProjectRepository projectRepository)
    {
        _projectRepository = projectRepository;
    }

    public async Task<Project> CreateAsync(Project project)
    {
        return await _projectRepository.CreateAsync(project);
    }
}
