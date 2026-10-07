using System.ComponentModel.DataAnnotations;
using Bit.Core.Repositories;
using Bit.Core.Vault.Enums;
using Bit.Core.Vault.Repositories;
using Bit.Seeder.Factories;
using Bit.Seeder.Models;
using Bit.Seeder.Services;

namespace Bit.Seeder.Scenes;

public class OrganizationBankAccountCipherScene(
    IOrganizationRepository organizationRepository,
    ICipherRepository cipherRepository,
    IManglerService manglerService)
    : IScene<OrganizationBankAccountCipherScene.Request, OrganizationBankAccountCipherScene.Result>
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
            Reprompt = request.Reprompt ? CipherRepromptType.Password : CipherRepromptType.None,
            EncryptionKey = request.OrganizationKeyB64,
            OrganizationId = request.OrganizationId,
            UserId = null,
            BankAccount = bankAccount
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
