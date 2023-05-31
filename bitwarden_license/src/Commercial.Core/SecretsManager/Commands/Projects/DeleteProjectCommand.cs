using Bit.Core.SecretsManager.Commands.Projects.Interfaces;
using Bit.Core.SecretsManager.Entities;
using Bit.Core.SecretsManager.Repositories;

namespace Bit.Commercial.Core.SecretsManager.Commands.Projects;

public class DeleteProjectCommand : IDeleteProjectCommand
{
    private readonly IProjectRepository _projectRepository;

    public DeleteProjectCommand(IProjectRepository projectRepository)
    {
        _projectRepository = projectRepository;
    }

    public async Task DeleteProjects(ICollection<Project> projects)
    {
        await _projectRepository.DeleteManyByIdAsync(projects.Select(p => p.Id));
    }
}
