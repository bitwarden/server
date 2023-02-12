using Bit.Core.SecretsManager.Commands.Porting;
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

    public async Task ImportAsync(Guid organizationId, SMImport import)
    {
        var importedProjects = new List<Guid>();
        var importedSecrets = new List<Guid>();

        try
        {
            import = AssignNewIds(import);

            if (import.Projects.Any())
            {
                importedProjects = (await _projectRepository.ImportAsync(import.Projects.Select(p => new Project
                {
                    Id = p.Id,
                    OrganizationId = organizationId,
                    Name = p.Name,
                }))).Select(p => p.Id).ToList();
            }

            if (import.Secrets != null && import.Secrets.Any())
            {
                importedSecrets = (await _secretRepository.ImportAsync(import.Secrets.Select(s => new Secret
                {
                    Id = s.Id,
                    OrganizationId = organizationId,
                    Key = s.Key,
                    Value = s.Value,
                    Note = s.Note,
                    Projects = s.ProjectIds != null && s.ProjectIds.Any() ? s.ProjectIds.Select(id => new Project { Id = id }).ToList() : null,
                }))).Select(s => s.Id).ToList();
            }
        }
        catch (Exception)
        {
            if (importedProjects.Any())
            {
                await _projectRepository.DeleteManyByIdAsync(importedProjects);
            }

            if (importedSecrets.Any())
            {
                await _secretRepository.HardDeleteManyByIdAsync(importedSecrets);
            }

            throw new Exception("Error attempting import");
        }
    }

    public SMImport AssignNewIds(SMImport import)
    {
        var projects = new Dictionary<Guid, SMImport.InnerProject>();
        var secrets = new List<SMImport.InnerSecret>();

        if (import.Projects != null && import.Projects.Any())
        {
            projects = import.Projects.ToDictionary(
                p => p.Id,
                p => new SMImport.InnerProject { Id = Guid.NewGuid(), Name = p.Name }
            );
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
                    ProjectIds = secret.ProjectIds?.Select(id => projects[id].Id),
                });
            }
        }

        return new SMImport
        {
            Projects = projects.Values,
            Secrets = secrets,
        };
    }
}
