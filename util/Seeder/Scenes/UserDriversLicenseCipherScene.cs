using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Bit.Core.Repositories;
using Bit.Core.Vault.Enums;
using Bit.Core.Vault.Repositories;
using Bit.Seeder.Factories;
using Bit.Seeder.Models;
using Bit.Seeder.Services;

namespace Bit.Seeder.Scenes;

public class UserDriversLicenseCipherScene(IUserRepository userRepository, ICipherRepository cipherRepository, IManglerService manglerService) : IScene<UserDriversLicenseCipherScene.Request, UserDriversLicenseCipherScene.Result>
{
    public class Request
    {
        [Required]
        public required Guid UserId { get; set; }
        [Required]
        public required string UserKeyB64 { get; set; }
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
            EncryptionKey = request.UserKeyB64,
            UserId = request.UserId,
            DriversLicense = driversLicense
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
