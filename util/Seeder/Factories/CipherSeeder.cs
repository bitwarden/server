using System.Text.Json;
using Bit.Core.Vault.Enums;
using Bit.Core.Vault.Models.Data;
using Bit.Infrastructure.EntityFramework.Vault.Models;
using Bit.RustSDK;
using Bogus;

namespace Bit.Seeder.Factories;

public class CipherSeeder
{
    /// <summary>
    /// Creates a batch of ciphers with mixed types for an organization.
    /// Distribution: 40% Login, 25% Secure Note, 20% Card, 15% Identity
    /// </summary>
    public static List<Cipher> CreateCiphers(
        int count,
        Guid organizationId,
        string organizationKey,
        RustSdkService rustSdkService)
    {
        var ciphers = new List<Cipher>();
        var faker = new Faker();

        for (var i = 0; i < count; i++)
        {
            // Determine cipher type based on distribution
            var roll = faker.Random.Int(1, 100);
            var type = roll switch
            {
                <= 40 => CipherType.Login,          // 40%
                <= 65 => CipherType.SecureNote,     // 25%
                <= 85 => CipherType.Card,           // 20%
                _ => CipherType.Identity            // 15%
            };

            var cipher = CreateCipher(type, organizationId, organizationKey, rustSdkService, faker);
            ciphers.Add(cipher);
        }

        return ciphers;
    }

    /// <summary>
    /// Creates a single cipher of the specified type.
    /// </summary>
    public static Cipher CreateCipher(
        CipherType type,
        Guid organizationId,
        string organizationKey,
        RustSdkService rustSdkService,
        Faker? faker = null)
    {
        faker ??= new Faker();

        CipherData cipherData = type switch
        {
            CipherType.Login => GenerateLoginData(faker, organizationKey, rustSdkService),
            CipherType.SecureNote => GenerateSecureNoteData(faker, organizationKey, rustSdkService),
            CipherType.Card => GenerateCardData(faker, organizationKey, rustSdkService),
            CipherType.Identity => GenerateIdentityData(faker, organizationKey, rustSdkService),
            _ => throw new ArgumentException($"Unsupported cipher type: {type}")
        };

        // Serialize to JSON (fields within are encrypted, but the JSON structure itself is plain)
        var json = JsonSerializer.Serialize(cipherData, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        return new Cipher
        {
            Id = Guid.NewGuid(),
            Type = type,
            OrganizationId = organizationId,
            UserId = null,  // Organization-owned
            Data = json,
            CreationDate = DateTime.UtcNow,
            RevisionDate = DateTime.UtcNow,
            Key = null,
            Reprompt = null,
            DeletedDate = null,
            ArchivedDate = null,
            Favorites = null,
            Folders = null,
            Attachments = null
        };
    }

    private static CipherLoginData GenerateLoginData(Faker faker, string organizationKey, RustSdkService rustSdkService)
    {
        var hasTotp = faker.Random.Float() < 0.1f;  // 10% chance of TOTP
        var hasNotes = faker.Random.Float() < 0.3f;

        return new CipherLoginData
        {
            Name = rustSdkService.EncryptString(faker.Internet.DomainName(), organizationKey),
            Username = rustSdkService.EncryptString(faker.Internet.Email(), organizationKey),
            Password = rustSdkService.EncryptString(faker.Internet.Password(16, memorable: false), organizationKey),
            Uris = new[]
            {
                new CipherLoginData.CipherLoginUriData
                {
                    Uri = rustSdkService.EncryptString(faker.Internet.Url(), organizationKey),
                    Match = Core.Enums.UriMatchType.Domain
                }
            },
            Totp = hasTotp ? rustSdkService.EncryptString(faker.Random.String2(32, "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567"), organizationKey) : null,
            Notes = hasNotes ? rustSdkService.EncryptString(faker.Lorem.Paragraph(), organizationKey) : null,
            PasswordRevisionDate = DateTime.UtcNow,
            AutofillOnPageLoad = null,
            Fido2Credentials = null
        };
    }

    private static CipherSecureNoteData GenerateSecureNoteData(Faker faker, string organizationKey, RustSdkService rustSdkService)
    {
        return new CipherSecureNoteData
        {
            Name = rustSdkService.EncryptString(faker.Hacker.Phrase(), organizationKey),
            Notes = rustSdkService.EncryptString(faker.Lorem.Paragraphs(faker.Random.Int(1, 3)), organizationKey),
            Type = SecureNoteType.Generic
        };
    }

    private static CipherCardData GenerateCardData(Faker faker, string organizationKey, RustSdkService rustSdkService)
    {
        var expYear = faker.Date.Future(5).Year;
        var expMonth = faker.Random.Int(1, 12);
        var hasNotes = faker.Random.Float() < 0.2f;

        // Common credit card brands
        var cardBrands = new[] { "Visa", "Mastercard", "American Express", "Discover", "Diners Club", "JCB" };

        return new CipherCardData
        {
            Name = rustSdkService.EncryptString($"{faker.Finance.AccountName()} Card", organizationKey),
            CardholderName = rustSdkService.EncryptString(faker.Name.FullName(), organizationKey),
            Brand = rustSdkService.EncryptString(faker.PickRandom(cardBrands), organizationKey),
            Number = rustSdkService.EncryptString(faker.Finance.CreditCardNumber(), organizationKey),
            ExpMonth = rustSdkService.EncryptString(expMonth.ToString("D2"), organizationKey),
            ExpYear = rustSdkService.EncryptString(expYear.ToString(), organizationKey),
            Code = rustSdkService.EncryptString(faker.Finance.CreditCardCvv(), organizationKey),
            Notes = hasNotes ? rustSdkService.EncryptString(faker.Lorem.Sentence(), organizationKey) : null
        };
    }

    private static CipherIdentityData GenerateIdentityData(Faker faker, string organizationKey, RustSdkService rustSdkService)
    {
        var hasMiddleName = faker.Random.Float() < 0.5f;
        var hasAddress2 = faker.Random.Float() < 0.3f;
        var hasPassport = faker.Random.Float() < 0.3f;
        var hasLicense = faker.Random.Float() < 0.4f;
        var hasNotes = faker.Random.Float() < 0.2f;

        return new CipherIdentityData
        {
            Name = rustSdkService.EncryptString(faker.Name.FullName(), organizationKey),
            Title = rustSdkService.EncryptString(faker.Name.Prefix(), organizationKey),
            FirstName = rustSdkService.EncryptString(faker.Name.FirstName(), organizationKey),
            MiddleName = hasMiddleName ? rustSdkService.EncryptString(faker.Name.FirstName(), organizationKey) : null,
            LastName = rustSdkService.EncryptString(faker.Name.LastName(), organizationKey),
            Address1 = rustSdkService.EncryptString(faker.Address.StreetAddress(), organizationKey),
            Address2 = hasAddress2 ? rustSdkService.EncryptString(faker.Address.SecondaryAddress(), organizationKey) : null,
            Address3 = null,
            City = rustSdkService.EncryptString(faker.Address.City(), organizationKey),
            State = rustSdkService.EncryptString(faker.Address.State(), organizationKey),
            PostalCode = rustSdkService.EncryptString(faker.Address.ZipCode(), organizationKey),
            Country = rustSdkService.EncryptString(faker.Address.Country(), organizationKey),
            Company = rustSdkService.EncryptString(faker.Company.CompanyName(), organizationKey),
            Email = rustSdkService.EncryptString(faker.Internet.Email(), organizationKey),
            Phone = rustSdkService.EncryptString(faker.Phone.PhoneNumber(), organizationKey),
            SSN = rustSdkService.EncryptString(faker.Random.Replace("###-##-####"), organizationKey),
            Username = rustSdkService.EncryptString(faker.Internet.UserName(), organizationKey),
            PassportNumber = hasPassport ? rustSdkService.EncryptString(faker.Random.Replace("?#######"), organizationKey) : null,
            LicenseNumber = hasLicense ? rustSdkService.EncryptString(faker.Random.Replace("?########"), organizationKey) : null,
            Notes = hasNotes ? rustSdkService.EncryptString(faker.Lorem.Sentence(), organizationKey) : null
        };
    }
}
