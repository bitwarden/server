using System.ComponentModel.DataAnnotations;
using Bit.Core.Repositories;
using Bit.Core.SecretsManager.Repositories;
using Bit.Seeder.Extensions;
using Bit.Seeder.Factories;
using Bit.Seeder.Services;

namespace Bit.Seeder.Scenes;

/// <summary>
/// Creates a Secrets Manager secret (key/value/note encrypted with the organization's symmetric key)
/// for an existing Secrets Manager-enabled organization, optionally associating it with projects.
/// </summary>
public class OrganizationSecretScene(
    IOrganizationRepository organizationRepository,
    ISecretRepository secretRepository,
    IProjectRepository projectRepository,
    IManglerService manglerService) : IScene<OrganizationSecretScene.Request, OrganizationSecretScene.Result>
{
    public class Request
    {
        [Required]
        public required Guid OrganizationId { get; set; }
        [Required]
        public required string OrganizationKeyB64 { get; set; }
        [Required]
        public required string Key { get; set; }
        public string? Value { get; set; }
        public string? Note { get; set; }
        public IEnumerable<Guid>? ProjectIds { get; set; }
    }

    public class Result
    {
        public required Guid SecretId { get; init; }
    }

    public async Task<SceneResult<Result>> SeedAsync(Request request)
    {
        var organization = await organizationRepository.GetSecretsManagerOrganizationOrThrowAsync(request.OrganizationId);

        await projectRepository.ThrowIfProjectsNotInOrganizationAsync(request.ProjectIds, organization.Id);

        var secret = SecretSeeder.Create(
            organization.Id,
            request.OrganizationKeyB64,
            request.Key,
            request.Value,
            request.Note,
            request.ProjectIds);

        var created = await secretRepository.CreateAsync(secret);

        return new SceneResult<Result>(
            result: new Result
            {
                SecretId = created.Id
            },
            mangleMap: manglerService.GetMangleMap());
    }
}
