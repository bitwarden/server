using System.ComponentModel.DataAnnotations;
using Bit.Core.Repositories;
using Bit.Core.Vault.Enums;
using Bit.Core.Vault.Repositories;
using Bit.Seeder.Factories;
using Bit.Seeder.Models;
using Bit.Seeder.Services;

namespace Bit.Seeder.Scenes;

public class UserIdentityCipherScene(IUserRepository userRepository, ICipherRepository cipherRepository, IManglerService manglerService) : IScene<UserIdentityCipherScene.Request, UserIdentityCipherScene.Result>
{
    public class Request
    {
        [Required]
        public required Guid UserId { get; set; }
        [Required]
        public required string UserKeyB64 { get; set; }
        [Required]
        public required string Name { get; set; }
        public string? Title { get; set; }
        public string? FirstName { get; set; }
        public string? MiddleName { get; set; }
        public string? LastName { get; set; }
        public string? Username { get; set; }
        public string? Company { get; set; }
        public string? SSN { get; set; }
        public string? PassportNumber { get; set; }
        public string? LicenseNumber { get; set; }
        public string? Email { get; set; }
        public string? Phone { get; set; }
        public string? Address1 { get; set; }
        public string? Address2 { get; set; }
        public string? Address3 { get; set; }
        public string? City { get; set; }
        public string? State { get; set; }
        public string? PostalCode { get; set; }
        public string? Country { get; set; }
        public string? Notes { get; set; }
        public bool Reprompt { get; set; }
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

        var identity = new IdentityViewDto
        {
            Title = request.Title,
            FirstName = request.FirstName,
            MiddleName = request.MiddleName,
            LastName = request.LastName,
            Username = request.Username,
            Company = request.Company,
            SSN = request.SSN,
            PassportNumber = request.PassportNumber,
            LicenseNumber = request.LicenseNumber,
            Email = request.Email,
            Phone = request.Phone,
            Address1 = request.Address1,
            Address2 = request.Address2,
            Address3 = request.Address3,
            City = request.City,
            State = request.State,
            PostalCode = request.PostalCode,
            Country = request.Country,
        };
        var cipher = IdentityCipherSeeder.Create(new CipherSeed
        {
            Type = CipherType.Identity,
            Name = request.Name,
            Notes = request.Notes,
            EncryptionKey = request.UserKeyB64,
            UserId = request.UserId,
            Identity = identity
        });
        if (request.Reprompt)
        {
            cipher.Reprompt = CipherRepromptType.Password;
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
