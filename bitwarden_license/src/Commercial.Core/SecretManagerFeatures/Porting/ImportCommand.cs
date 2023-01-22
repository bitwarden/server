using Bit.Core.Entities;
using Bit.Core.Repositories;
using Bit.Core.SecretManagerFeatures.Porting.Interfaces;

namespace Bit.Commercial.Core.SecretManagerFeatures.Projects;

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
            AssignNewIds(import);

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

    public void AssignNewIds(SMImport import)
    {
        Dictionary<Guid, Guid> oldNewProjectIds = new Dictionary<Guid, Guid>();

        if (import.Projects != null && import.Projects.Any())
        {
            foreach (var project in import.Projects)
            {
                var newProjectId = new Guid();
                oldNewProjectIds.Add(project.Id, newProjectId);
                project.Id = newProjectId;
            }
        }

        if (import.Secrets != null && import.Secrets.Any())
        {
            foreach (var secret in import.Secrets)
            {
                secret.Id = new Guid();
                secret.ProjectIds = secret.ProjectIds?.Select(projectId => oldNewProjectIds[projectId]);
            }
        }
    }
}
