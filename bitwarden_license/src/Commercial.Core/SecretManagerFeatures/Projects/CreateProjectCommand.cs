using Bit.Core.Entities;
using Bit.Core.Repositories;
using Bit.Core.SecretManagerFeatures.Projects.Interfaces;

namespace Bit.Commercial.Core.SecretManagerFeatures.Projects;

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
