using Bit.Core.SecretsManager.Commands.Porting.Interfaces;
using Bit.Core.SecretsManager.Entities;
using Bit.Core.SecretsManager.Repositories;

namespace Bit.Commercial.Core.SecretsManager.Commands.Porting;

public class ImportCommand : IImportCommand
{
    private readonly IProjectRepository _projectRepository;
    private readonly ISecretRepository _secretRepository;

    public ImportCommand(IProjectRepository projectRepository, ISecretRepository secretRepository)
    {
        _projectRepository = projectRepository;
        _secretRepository = secretRepository;
    }

    public async Task<SMImport> ImportAsync(Guid organizationId, SMImport import)
    {
        try
        {
            import = AssignNewIds(import);

            if (import.Projects != null && import.Projects.Any())
            {
                await _projectRepository.ImportAsync(import.Projects.Select(p => new Project
                {
                    Id = p.Id,
                    OrganizationId = organizationId,
                    Name = p.Name,
                }));
            }

            if (import.Secrets != null && import.Secrets.Any())
            {
                await _secretRepository.ImportAsync(import.Secrets.Select(s => new Secret
                {
                    Id = s.Id,
                    OrganizationId = organizationId,
                    Key = s.Key,
                    Value = s.Value,
                    Note = s.Note,
                    Projects = s.ProjectIds != null && s.ProjectIds.Any() ? s.ProjectIds.Select(id => new Project { Id = id }).ToList() : null,
                }));
            }
        }
        catch (Exception)
        {
            throw new Exception("Error attempting import");
        }

        return import;
    }

    public SMImport AssignNewIds(SMImport import)
    {
        Dictionary<Guid, Guid> oldNewProjectIds = new Dictionary<Guid, Guid>();
        var projects = new List<SMImport.InnerProject>();
        var secrets = new List<SMImport.InnerSecret>();

        if (import.Projects != null && import.Projects.Any())
        {
            foreach (var project in import.Projects)
            {
                var newProjectId = Guid.NewGuid();
                oldNewProjectIds.Add(project.Id, newProjectId);
                projects.Add(new SMImport.InnerProject
                {
                    Id = newProjectId,
                    Name = project.Name,
                });
            }
        }

        if (import.Secrets != null && import.Secrets.Any())
        {
            foreach (var secret in import.Secrets)
            {
                secrets.Add(new SMImport.InnerSecret
                {
                    Id = Guid.NewGuid(),
                    Key = secret.Key,
                    Value = secret.Value,
                    Note = secret.Note,
                    ProjectIds = secret.ProjectIds?.Select(projectId => oldNewProjectIds[projectId]),
                });
            }
        }

        return new SMImport
        {
            Projects = projects,
            Secrets = secrets
        };
    }
}
