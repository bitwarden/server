using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Bit.Core.Repositories;
using Bit.Core.Vault.Enums;
using Bit.Core.Vault.Repositories;
using Bit.Seeder.Factories;
using Bit.Seeder.Models;
using Bit.Seeder.Services;

namespace Bit.Seeder.Scenes;

public class UserPassportCipherScene(IUserRepository userRepository, ICipherRepository cipherRepository, IManglerService manglerService) : IScene<UserPassportCipherScene.Request, UserPassportCipherScene.Result>
{
    public class Request
    {
        [Required]
        public required Guid UserId { get; set; }
        [Required]
        public required string UserKeyB64 { get; set; }
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
            EncryptionKey = request.UserKeyB64,
            UserId = request.UserId,
            Passport = passport
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
