using System.ComponentModel.DataAnnotations;
using Bit.Core.Repositories;
using Bit.Core.Vault.Enums;
using Bit.Core.Vault.Repositories;
using Bit.Seeder.Factories;
using Bit.Seeder.Models;
using Bit.Seeder.Services;

namespace Bit.Seeder.Scenes;

public class OrganizationPassportCipherScene(
    IOrganizationRepository organizationRepository,
    ICipherRepository cipherRepository,
    IManglerService manglerService)
    : IScene<OrganizationPassportCipherScene.Request, OrganizationPassportCipherScene.Result>
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
        public string? Surname { get; set; }
        public string? GivenName { get; set; }
        public string? DateOfBirth { get; set; }
        public string? Sex { get; set; }
        public string? BirthPlace { get; set; }
        public string? Nationality { get; set; }
        public string? PassportNumber { get; set; }
        public string? PassportType { get; set; }
        public string? IssuingCountry { get; set; }
        public string? IssuingAuthority { get; set; }
        public string? IssueDate { get; set; }
        public string? ExpirationDate { get; set; }
        public string? NationalIdentificationNumber { get; set; }
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

        var passport = new PassportViewDto
        {
            Surname = request.Surname,
            GivenName = request.GivenName,
            DateOfBirth = request.DateOfBirth,
            Sex = request.Sex,
            BirthPlace = request.BirthPlace,
            Nationality = request.Nationality,
            PassportNumber = request.PassportNumber,
            PassportType = request.PassportType,
            IssuingCountry = request.IssuingCountry,
            IssuingAuthority = request.IssuingAuthority,
            IssueDate = request.IssueDate,
            ExpirationDate = request.ExpirationDate,
            NationalIdentificationNumber = request.NationalIdentificationNumber
        };
        var cipher = PassportCipherSeeder.Create(new CipherSeed
        {
            Type = CipherType.Passport,
            Name = request.Name,
            Notes = request.Notes,
            Reprompt = request.Reprompt ? CipherRepromptType.Password : CipherRepromptType.None,
            EncryptionKey = request.OrganizationKeyB64,
            OrganizationId = request.OrganizationId,
            UserId = null,
            Passport = passport
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
