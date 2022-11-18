using Bit.Core.Entities;
using Bit.Core.Repositories;
using Bit.Core.SecretManagerFeatures.Secrets.Interfaces;

namespace Bit.Commercial.Core.SecretManagerFeatures.Secrets;

public class CreateSecretCommand : ICreateSecretCommand
{
    private readonly ISecretRepository _secretRepository;

    public CreateSecretCommand(ISecretRepository secretRepository)
    {
        _secretRepository = secretRepository;
    }

    public async Task<Secret> CreateAsync(Secret secret, Guid? projectId)
    {
        if (projectId.HasValue)
        {
            secret.Projects = new List<Project>();
            var p = new Project
            {
                Id = projectId.Value
            };
            secret.Projects.Add(p);
        }

        return await _secretRepository.CreateAsync(secret, projectId);
    }
}
