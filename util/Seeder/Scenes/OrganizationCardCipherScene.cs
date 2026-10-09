using System.ComponentModel.DataAnnotations;
using Bit.Core.Repositories;
using Bit.Core.Vault.Enums;
using Bit.Core.Vault.Repositories;
using Bit.Seeder.Factories;
using Bit.Seeder.Models;
using Bit.Seeder.Services;

namespace Bit.Seeder.Scenes;

public class OrganizationCardCipherScene(
    IOrganizationRepository organizationRepository,
    ICipherRepository cipherRepository,
    IManglerService manglerService)
    : IScene<OrganizationCardCipherScene.Request, OrganizationCardCipherScene.Result>
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
        public required string CardholderName { get; set; }
        public required string Number { get; set; }
        public required string ExpMonth { get; set; }
        public required string ExpYear { get; set; }
        public required string Code { get; set; }
        public string? Brand { get; set; }
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

        var card = new CardViewDto
        {
            CardholderName = request.CardholderName,
            Brand = request.Brand,
            Number = request.Number,
            ExpMonth = request.ExpMonth,
            ExpYear = request.ExpYear,
            Code = request.Code
        };
        var cipher = CardCipherSeeder.Create(new CipherSeed
        {
            Type = CipherType.Card,
            Name = request.Name,
            Notes = request.Notes,
            Reprompt = request.Reprompt ? CipherRepromptType.Password : CipherRepromptType.None,
            EncryptionKey = request.OrganizationKeyB64,
            OrganizationId = request.OrganizationId,
            UserId = null,
            Card = card
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
