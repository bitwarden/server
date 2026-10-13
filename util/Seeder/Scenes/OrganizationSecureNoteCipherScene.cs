using System.ComponentModel.DataAnnotations;
using Bit.Core.Repositories;
using Bit.Core.Vault.Enums;
using Bit.Core.Vault.Repositories;
using Bit.Seeder.Factories;
using Bit.Seeder.Models;
using Bit.Seeder.Services;

namespace Bit.Seeder.Scenes;

public class OrganizationSecureNoteCipherScene(
    IOrganizationRepository organizationRepository,
    ICipherRepository cipherRepository,
    IManglerService manglerService)
    : IScene<OrganizationSecureNoteCipherScene.Request, OrganizationSecureNoteCipherScene.Result>
{
    public class Request
    {
        [Required]
        public required Guid OrganizationId { get; set; }
        [Required]
        public required string OrganizationKeyB64 { get; set; }
        [Required]
        public required IEnumerable<Guid> CollectionIds { get; set; }
        [Required]
        public required string Name { get; set; }
        public string? Notes { get; set; }
        public bool Reprompt { get; set; }
    }

    public class Result
    {
        public required Guid CipherId { get; init; }
    }

    public async Task<SceneResult<Result>> SeedAsync(Request request)
    {
        var organization = await organizationRepository.GetByIdAsync(request.OrganizationId);
        if (organization == null)
        {
            throw new InvalidOperationException($"Organization {request.OrganizationId} not found.");
        }

        var cipher = SecureNoteCipherSeeder.Create(new CipherSeed
        {
            Type = CipherType.SecureNote,
            Name = request.Name,
            Notes = request.Notes,
            Reprompt = request.Reprompt ? CipherRepromptType.Password : CipherRepromptType.None,
            EncryptionKey = request.OrganizationKeyB64,
            OrganizationId = request.OrganizationId,
            UserId = null
        });

        await cipherRepository.CreateAsync(cipher, request.CollectionIds);

        return new SceneResult<Result>(
            result: new Result
            {
                CipherId = cipher.Id
            },
            mangleMap: manglerService.GetMangleMap());
    }
}
