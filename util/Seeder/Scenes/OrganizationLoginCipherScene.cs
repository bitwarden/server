using System.ComponentModel.DataAnnotations;
using Bit.Core.Repositories;
using Bit.Core.Vault.Enums;
using Bit.Core.Vault.Repositories;
using Bit.Seeder.Factories;
using Bit.Seeder.Models;
using Bit.Seeder.Services;

namespace Bit.Seeder.Scenes;

/// <summary>
/// Creates an organization-owned login cipher encrypted with the organization's symmetric key and
/// assigns it to the requested collections. Mirrors <see cref="UserLoginCipherScene"/> for org vaults.
/// </summary>
public class OrganizationLoginCipherScene(
    IOrganizationRepository organizationRepository,
    ICipherRepository cipherRepository,
    IManglerService manglerService) : IScene<OrganizationLoginCipherScene.Request, OrganizationLoginCipherScene.Result>
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
        public string? Username { get; set; }
        public string? Password { get; set; }
        public string? Totp { get; set; }
        public string? Uri { get; set; }
        public string? Notes { get; set; }
        public bool Reprompt { get; set; }
        public bool Deleted { get; set; }
        public IEnumerable<FieldRequest>? Fields { get; set; }
        public IEnumerable<PasskeyRequest>? Passkeys { get; set; }
    }

    public class FieldRequest
    {
        public required string Name { get; set; }
        public required string Value { get; set; }
        public required int Type { get; set; }
    }

    public class PasskeyRequest
    {
        public required string RpId { get; set; }
        public required string RpName { get; set; }
        public required string UserName { get; set; }
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

        var cipher = LoginCipherSeeder.Create(new CipherSeed
        {
            Type = CipherType.Login,
            Name = request.Name,
            Notes = request.Notes,
            EncryptionKey = request.OrganizationKeyB64,
            OrganizationId = organization.Id,
            Login = new LoginViewDto
            {
                Username = request.Username,
                Password = request.Password,
                Totp = request.Totp,
                Uris = string.IsNullOrEmpty(request.Uri) ? null : [new LoginUriViewDto { Uri = request.Uri }],
                Fido2Credentials = request.Passkeys?.Select(p => LoginCipherSeeder.CreateFido2Credential(p.RpId, p.RpName, p.UserName)).ToList()
            },
            Fields = request.Fields?.Select(f => new FieldViewDto
            {
                Name = f.Name,
                Value = f.Value,
                Type = f.Type
            }).ToList()
        });
        if (request.Reprompt)
        {
            cipher.Reprompt = CipherRepromptType.Password;
        }
        if (request.Deleted)
        {
            cipher.DeletedDate = DateTime.UtcNow.AddDays(-1);
        }

        await cipherRepository.CreateAsync(cipher, request.CollectionIds);

        return new SceneResult<Result>(
            result: new Result
            {
                CipherId = cipher.Id
            },
            mangleMap: manglerService.GetMangleMap());
    }
}
