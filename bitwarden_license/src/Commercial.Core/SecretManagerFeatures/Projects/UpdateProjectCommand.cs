using Bit.Core.Entities;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Core.SecretManagerFeatures.Projects.Interfaces;

namespace Bit.Commercial.Core.SecretManagerFeatures.Projects;

public class UpdateProjectCommand : IUpdateProjectCommand
{
    private readonly IProjectRepository _projectRepository;

    public UpdateProjectCommand(IProjectRepository projectRepository)
    {
        _projectRepository = projectRepository;
    }

    public async Task<Project> UpdateAsync(Project project)
    {
        var existingProject = await _projectRepository.GetByIdAsync(project.Id);
        if (existingProject == null)
        {
            throw new NotFoundException();
        }

        project.OrganizationId = existingProject.OrganizationId;
        project.CreationDate = existingProject.CreationDate;
        project.DeletedDate = existingProject.DeletedDate;
        project.RevisionDate = DateTime.UtcNow;

        await _projectRepository.ReplaceAsync(project);
        return project;
    }
}
