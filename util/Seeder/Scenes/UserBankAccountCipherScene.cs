using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Bit.Core.Repositories;
using Bit.Core.Vault.Enums;
using Bit.Core.Vault.Repositories;
using Bit.Seeder.Factories;
using Bit.Seeder.Models;
using Bit.Seeder.Services;

namespace Bit.Seeder.Scenes;

public class UserBankAccountCipherScene(IUserRepository userRepository, ICipherRepository cipherRepository, IManglerService manglerService) : IScene<UserBankAccountCipherScene.Request, UserBankAccountCipherScene.Result>
{
    public class Request
    {
        [Required]
        public required Guid UserId { get; set; }
        [Required]
        public required string UserKeyB64 { get; set; }
        [Required]
        public required string Name { get; set; }
        public string? BankName { get; set; }
        public string? NameOnAccount { get; set; }
        public string? AccountType { get; set; }
        public string? AccountNumber { get; set; }
        public string? RoutingNumber { get; set; }
        public string? BranchNumber { get; set; }
        public string? Pin { get; set; }
        public string? SwiftCode { get; set; }
        public string? Iban { get; set; }
        public string? BankContactPhone { get; set; }
        public string? Notes { get; set; }
        public bool Reprompt { get; set; }
        public bool Favorite { get; set; }
        public Guid? FolderId { get; set; }
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

        var bankAccount = new BankAccountViewDto
        {
            BankName = request.BankName,
            NameOnAccount = request.NameOnAccount,
            AccountType = request.AccountType,
            AccountNumber = request.AccountNumber,
            RoutingNumber = request.RoutingNumber,
            BranchNumber = request.BranchNumber,
            Pin = request.Pin,
            SwiftCode = request.SwiftCode,
            Iban = request.Iban,
            BankContactPhone = request.BankContactPhone
        };
        var cipher = BankAccountCipherSeeder.Create(new CipherSeed
        {
            Type = CipherType.BankAccount,
            Name = request.Name,
            Notes = request.Notes,
            EncryptionKey = request.UserKeyB64,
            UserId = request.UserId,
            BankAccount = bankAccount
        });
        if (request.Reprompt)
        {
            cipher.Reprompt = CipherRepromptType.Password;
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

        await cipherRepository.CreateAsync(cipher);

        return new SceneResult<Result>(
            result: new Result
            {
                CipherId = cipher.Id
            },
            mangleMap: manglerService.GetMangleMap());
    }
}
