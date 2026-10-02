using System.ComponentModel.DataAnnotations;
using Bit.Core.Repositories;
using Bit.Core.Vault.Enums;
using Bit.Core.Vault.Repositories;
using Bit.Seeder.Factories;
using Bit.Seeder.Models;
using Bit.Seeder.Services;

namespace Bit.Seeder.Scenes;

public class OrganizationDriversLicenseCipherScene(
    IOrganizationRepository organizationRepository,
    ICipherRepository cipherRepository,
    IManglerService manglerService)
    : IScene<OrganizationDriversLicenseCipherScene.Request, OrganizationDriversLicenseCipherScene.Result>
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
        public string? FirstName { get; set; }
        public string? MiddleName { get; set; }
        public string? LastName { get; set; }
        public string? DateOfBirth { get; set; }
        public string? LicenseNumber { get; set; }
        public string? IssuingCountry { get; set; }
        public string? IssuingState { get; set; }
        public string? IssueDate { get; set; }
        public string? IssuingAuthority { get; set; }
        public string? ExpirationDate { get; set; }
        public string? LicenseClass { get; set; }
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

        var driversLicense = new DriversLicenseViewDto
        {
            FirstName = request.FirstName,
            MiddleName = request.MiddleName,
            LastName = request.LastName,
            DateOfBirth = request.DateOfBirth,
            LicenseNumber = request.LicenseNumber,
            IssuingCountry = request.IssuingCountry,
            IssuingState = request.IssuingState,
            IssueDate = request.IssueDate,
            IssuingAuthority = request.IssuingAuthority,
            ExpirationDate = request.ExpirationDate,
            LicenseClass = request.LicenseClass
        };
        var cipher = DriversLicenseCipherSeeder.Create(new CipherSeed
        {
            Type = CipherType.DriversLicense,
            Name = request.Name,
            Notes = request.Notes,
            Reprompt = request.Reprompt ? CipherRepromptType.Password : CipherRepromptType.None,
            EncryptionKey = request.OrganizationKeyB64,
            OrganizationId = request.OrganizationId,
            UserId = null,
            DriversLicense = driversLicense
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
