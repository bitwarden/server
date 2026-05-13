using System.ComponentModel.DataAnnotations;
using Bit.Core.Repositories;
using Bit.Core.Vault.Enums;
using Bit.Core.Vault.Repositories;
using Bit.Seeder.Factories;
using Bit.Seeder.Models;
using Bit.Seeder.Services;

namespace Bit.Seeder.Scenes;

public class UserCardCipherScene(IUserRepository userRepository, ICipherRepository cipherRepository, IManglerService manglerService) : IScene<UserCardCipherScene.Request, UserCardCipherScene.Result>
{
    public class Request
    {
        [Required]
        public required Guid UserId { get; set; }
        [Required]
        public required string UserKeyB64 { get; set; }
        [Required]
        public required string Name { get; set; }
        public required string CardholderName { get; set; }
        public required string Number { get; set; }
        public required string ExpMonth { get; set; }
        public required string ExpYear { get; set; }
        public required string Code { get; set; }
        public string? Notes { get; set; }
        public bool Reprompt { get; set; }
    }

    public class Result
    {
        public required Guid CipherId { get; init; }
    }

    public async Task<SceneResult<Result>> SeedAsync(Request request)
    {
        var user = await userRepository.GetByIdAsync(request.UserId);
        if (user == null)
        {
            throw new Exception($"User with ID {request.UserId} not found.");
        }

        var card = new CardViewDto
        {
            CardholderName = request.CardholderName,
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
            EncryptionKey = request.UserKeyB64,
            UserId = request.UserId,
            Card = card
        });
        if (request.Reprompt)
        {
            cipher.Reprompt = CipherRepromptType.Password;
        }

        await cipherRepository.CreateAsync(cipher);

        return new SceneResult<Result>(
            result: new Result
            {
                CipherId = cipher.Id
            },
            mangleMap: manglerService.GetMangleMap());
    }
}
