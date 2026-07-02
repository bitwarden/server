using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Bit.Core.Vault.Entities;
using Bit.Core.Vault.Enums;
using Bit.Seeder.Factories;
using Bit.Seeder.Models;
using Bit.Seeder.Services;

namespace Bit.Seeder.Scenes;

/// <summary>
/// Base scene for seeding a login cipher. Builds and persists the cipher from the shared
/// <see cref="LoginCipherRequest"/> fields, deferring the owner-specific bits (key resolution,
/// owner-only customizations, and the save call) to the derived scene.
/// </summary>
public abstract class LoginCipherScene<TRequest>(IManglerService manglerService)
    : IScene<TRequest, LoginCipherScene<TRequest>.Result>
    where TRequest : LoginCipherScene<TRequest>.LoginCipherRequest
{
    public abstract class LoginCipherRequest
    {
        [Required]
        public required Guid UserId { get; set; }
        [Required]
        public required string Name { get; set; }
        public string? Username { get; set; }
        public string? Password { get; set; }
        public string? Totp { get; set; }
        public string? Uri { get; set; }
        public string? Notes { get; set; }
        public bool Reprompt { get; set; }
        public bool Deleted { get; set; }
        public bool Favorite { get; set; }
        public Guid? FolderId { get; set; }
        public IEnumerable<FieldRequest>? Fields { get; set; }
        public IEnumerable<PasskeyRequest>? Passkeys { get; set; }

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
    }

    public class Result
    {
        public required Guid CipherId { get; init; }
    }

    /// <summary>Owner resolution result: which key encrypts the cipher and which owner id it carries.</summary>
    protected record CipherOwner(string EncryptionKey, Guid? OrganizationId, Guid? UserId);

    /// <summary>Look up + validate the owner, returning the encryption key and owner ids.</summary>
    protected abstract Task<CipherOwner> ResolveOwnerAsync(TRequest request);

    /// <summary>Persist the cipher (with or without collection links).</summary>
    protected abstract Task PersistAsync(Cipher cipher, TRequest request);

    public async Task<SceneResult<Result>> SeedAsync(TRequest request)
    {
        var owner = await ResolveOwnerAsync(request);

        var cipher = LoginCipherSeeder.Create(new CipherSeed
        {
            Type = CipherType.Login,
            Name = request.Name,
            Notes = request.Notes,
            EncryptionKey = owner.EncryptionKey,
            OrganizationId = owner.OrganizationId,
            UserId = owner.UserId,
            Login = new LoginViewDto
            {
                Username = request.Username,
                Password = request.Password,
                Totp = request.Totp,
                Uris = string.IsNullOrEmpty(request.Uri) ? null : [new LoginUriViewDto { Uri = request.Uri }],
                Fido2Credentials = request.Passkeys?
                    .Select(p => LoginCipherSeeder.CreateFido2Credential(p.RpId, p.RpName, p.UserName)).ToList()
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
        if (request.Favorite)
        {
            cipher.Favorites = JsonSerializer.Serialize(new Dictionary<string, bool>
            {
                { request.UserId.ToString().ToUpperInvariant(), true }
            });
        }
        if (request.FolderId.HasValue)
        {
            cipher.Folders = CipherComposer.BuildFoldersJson(new Dictionary<Guid, Guid>
            {
                { request.UserId, request.FolderId.Value }
            });
        }

        await PersistAsync(cipher, request);

        return new SceneResult<Result>(
            result: new Result { CipherId = cipher.Id },
            mangleMap: manglerService.GetMangleMap());
    }
}
