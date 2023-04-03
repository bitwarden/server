using Bit.Core.SecretsManager.Commands.Projects.Interfaces;
using Bit.Core.SecretsManager.Entities;
using Bit.Core.SecretsManager.Repositories;

namespace Bit.Commercial.Core.SecretsManager.Commands.Projects;

public class UpdateProjectCommand : IUpdateProjectCommand
{
    private readonly IProjectRepository _projectRepository;

    public UpdateProjectCommand(IProjectRepository projectRepository)
    {
        _projectRepository = projectRepository;
    }

    public async Task<Project> UpdateAsync(Project project, Project updatedProject)
    {
        project.Name = updatedProject.Name;
        project.RevisionDate = DateTime.UtcNow;

        await _projectRepository.ReplaceAsync(project);
        return project;
    }
}
