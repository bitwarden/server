using System.ComponentModel.DataAnnotations;
using Bit.Core.Repositories;
using Bit.Core.SecretsManager.Entities;
using Bit.Core.SecretsManager.Repositories;
using Bit.RustSDK;
using Bit.Seeder.Services;

namespace Bit.Seeder.Scenes;

/// <summary>
/// Creates a Secrets Manager secret (key/value/note encrypted with the organization's symmetric key)
/// for an existing Secrets Manager-enabled organization, optionally associating it with projects.
/// </summary>
public class OrganizationSecretScene(
    IOrganizationRepository organizationRepository,
    ISecretRepository secretRepository,
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
        var organization = await organizationRepository.GetByIdAsync(request.OrganizationId);
        if (organization == null)
        {
            throw new InvalidOperationException($"Organization {request.OrganizationId} not found.");
        }

        var secret = new Secret
        {
            OrganizationId = organization.Id,
            Key = RustSdkService.EncryptString(request.Key, request.OrganizationKeyB64),
            Value = RustSdkService.EncryptString(request.Value ?? string.Empty, request.OrganizationKeyB64),
            Note = RustSdkService.EncryptString(request.Note ?? string.Empty, request.OrganizationKeyB64),
            Projects = request.ProjectIds?
                .Select(id => new Project { Id = id, OrganizationId = organization.Id })
                .ToList()
        };

        var created = await secretRepository.CreateAsync(secret);

        return new SceneResult<Result>(
            result: new Result
            {
                SecretId = created.Id
            },
            mangleMap: manglerService.GetMangleMap());
    }
}
