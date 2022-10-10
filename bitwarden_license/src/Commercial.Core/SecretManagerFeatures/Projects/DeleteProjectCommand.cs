using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Core.SecretManagerFeatures.Projects.Interfaces;

namespace Bit.Commercial.Core.SecretManagerFeatures.Projects;

public class DeleteProjectCommand : IDeleteProjectCommand
{
    private readonly IProjectRepository _projectRepository;

    public DeleteProjectCommand(IProjectRepository projectRepository)
    {
        _projectRepository = projectRepository;
    }

    public async Task<List<Tuple<Guid, string>>> DeleteProjects(List<Guid> ids)
    {
        var projects = await _projectRepository.GetManyByIds(ids);

        if (projects?.Any() != true)
        {
            throw new NotFoundException();
        }

        var results = ids.Select(id =>
        {
            if (!projects.Any(project => project.Id == id))
            {
                throw new NotFoundException();
            }
            // TODO Once permissions are implemented add check for each project here.
            else
            {
                return new Tuple<Guid, string>(id, "");
            }
        }).ToList();

        await _projectRepository.DeleteManyByIdAsync(ids);
        return results;
    }
}

