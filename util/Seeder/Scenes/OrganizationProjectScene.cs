using System.ComponentModel.DataAnnotations;
using Bit.Core.Repositories;
using Bit.Core.SecretsManager.Repositories;
using Bit.Seeder.Extensions;
using Bit.Seeder.Factories;
using Bit.Seeder.Services;

namespace Bit.Seeder.Scenes;

/// <summary>
/// Creates a Secrets Manager project (name encrypted with the organization's symmetric key) for an
/// existing Secrets Manager-enabled organization.
/// </summary>
public class OrganizationProjectScene(
    IOrganizationRepository organizationRepository,
    IProjectRepository projectRepository,
    IManglerService manglerService) : IScene<OrganizationProjectScene.Request, OrganizationProjectScene.Result>
{
    public class Request
    {
        [Required]
        public required Guid OrganizationId { get; set; }
        [Required]
        public required string OrganizationKeyB64 { get; set; }
        [Required]
        public required string Name { get; set; }
    }

    public class Result
    {
        public required Guid ProjectId { get; init; }
    }

    public async Task<SceneResult<Result>> SeedAsync(Request request)
    {
        var organization = await organizationRepository.GetSecretsManagerOrganizationOrThrowAsync(request.OrganizationId);

        var project = ProjectSeeder.Create(organization.Id, request.OrganizationKeyB64, request.Name);

        var created = await projectRepository.CreateAsync(project);

        return new SceneResult<Result>(
            result: new Result
            {
                ProjectId = created.Id
            },
            mangleMap: manglerService.GetMangleMap());
    }
}
