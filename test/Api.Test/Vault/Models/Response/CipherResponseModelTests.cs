using System.Text.Json;
using Bit.Api.Vault.Models.Response;
using Bit.Core.Settings;
using Bit.Core.Vault.Authorization;
using Bit.Core.Vault.Entities;
using Bit.Core.Vault.Enums;
using Bit.Core.Vault.Models.Data;
using NSubstitute;
using Xunit;

namespace Bit.Api.Test.Vault.Models.Response;

public class CipherResponseModelTests
{
    private readonly IGlobalSettings _globalSettings;

    public CipherResponseModelTests()
    {
        _globalSettings = Substitute.For<IGlobalSettings>();
        var attachmentSettings = Substitute.For<IFileStorageSettings>();
        _globalSettings.Attachment.Returns(attachmentSettings);
    }

    [Fact]
    public void Constructor_DriversLicense_DeserializesAllFields()
    {
        var driversLicenseData = new CipherDriversLicenseData
        {
            Name = "2.name|encrypted",
            Notes = "2.notes|encrypted",
            FirstName = "2.firstName|encrypted",
            MiddleName = "2.middleName|encrypted",
            LastName = "2.lastName|encrypted",
            LicenseNumber = "2.licenseNumber|encrypted",
            IssuingCountry = "2.issuingCountry|encrypted",
            IssuingState = "2.issuingState|encrypted",
            ExpirationDate = "2.expirationDate|encrypted",
            LicenseClass = "2.licenseClass|encrypted",
        };

        var cipher = new Cipher
        {
            Id = Guid.NewGuid(),
            Type = CipherType.DriversLicense,
            Data = JsonSerializer.Serialize(driversLicenseData),
            RevisionDate = DateTime.UtcNow,
            CreationDate = DateTime.UtcNow,
        };

        var response = new FullCipherMiniResponseModel(FullCipherAccess.Unrestricted(), cipher, _globalSettings, false);

        Assert.Equal(CipherType.DriversLicense, response.Type);
        Assert.Equal("2.name|encrypted", response.Name);
        Assert.Equal("2.notes|encrypted", response.Notes);
        Assert.NotNull(response.DriversLicense);
        Assert.Equal("2.firstName|encrypted", response.DriversLicense.FirstName);
        Assert.Equal("2.middleName|encrypted", response.DriversLicense.MiddleName);
        Assert.Equal("2.lastName|encrypted", response.DriversLicense.LastName);
        Assert.Equal("2.licenseNumber|encrypted", response.DriversLicense.LicenseNumber);
        Assert.Equal("2.issuingCountry|encrypted", response.DriversLicense.IssuingCountry);
        Assert.Equal("2.issuingState|encrypted", response.DriversLicense.IssuingState);
        Assert.Equal("2.expirationDate|encrypted", response.DriversLicense.ExpirationDate);
        Assert.Equal("2.licenseClass|encrypted", response.DriversLicense.LicenseClass);
    }

    [Fact]
    public void Constructor_DriversLicense_WithMinimalData_DeserializesSuccessfully()
    {
        var driversLicenseData = new CipherDriversLicenseData
        {
            Name = "2.name|encrypted",
            LicenseNumber = "2.licenseNumber|encrypted",
        };

        var cipher = new Cipher
        {
            Id = Guid.NewGuid(),
            Type = CipherType.DriversLicense,
            Data = JsonSerializer.Serialize(driversLicenseData),
            RevisionDate = DateTime.UtcNow,
            CreationDate = DateTime.UtcNow,
        };

        var response = new FullCipherMiniResponseModel(FullCipherAccess.Unrestricted(), cipher, _globalSettings, false);

        Assert.Equal(CipherType.DriversLicense, response.Type);
        Assert.NotNull(response.DriversLicense);
        Assert.Equal("2.licenseNumber|encrypted", response.DriversLicense.LicenseNumber);
        Assert.Null(response.DriversLicense.FirstName);
        Assert.Null(response.DriversLicense.MiddleName);
        Assert.Null(response.DriversLicense.LastName);
    }

    [Fact]
    public void Constructor_Passport_DeserializesAllFields()
    {
        var passportData = new CipherPassportData
        {
            Name = "2.name|encrypted",
            Notes = "2.notes|encrypted",
            Surname = "2.surname|encrypted",
            GivenName = "2.givenName|encrypted",
            DateOfBirth = "2.dateOfBirth|encrypted",
            Nationality = "2.nationality|encrypted",
            PassportNumber = "2.passportNumber|encrypted",
            PassportType = "2.passportType|encrypted",
            IssuingCountry = "2.issuingCountry|encrypted",
            IssuingAuthority = "2.issuingAuthority|encrypted",
            IssueDate = "2.issueDate|encrypted",
            ExpirationDate = "2.expirationDate|encrypted",
        };

        var cipher = new Cipher
        {
            Id = Guid.NewGuid(),
            Type = CipherType.Passport,
            Data = JsonSerializer.Serialize(passportData),
            RevisionDate = DateTime.UtcNow,
            CreationDate = DateTime.UtcNow,
        };

        var response = new FullCipherMiniResponseModel(FullCipherAccess.Unrestricted(), cipher, _globalSettings, false);

        Assert.Equal(CipherType.Passport, response.Type);
        Assert.Equal("2.name|encrypted", response.Name);
        Assert.Equal("2.notes|encrypted", response.Notes);
        Assert.NotNull(response.Passport);
        Assert.Equal("2.surname|encrypted", response.Passport.Surname);
        Assert.Equal("2.givenName|encrypted", response.Passport.GivenName);
        Assert.Equal("2.dateOfBirth|encrypted", response.Passport.DateOfBirth);
        Assert.Equal("2.nationality|encrypted", response.Passport.Nationality);
        Assert.Equal("2.passportNumber|encrypted", response.Passport.PassportNumber);
        Assert.Equal("2.passportType|encrypted", response.Passport.PassportType);
        Assert.Equal("2.issuingCountry|encrypted", response.Passport.IssuingCountry);
        Assert.Equal("2.issuingAuthority|encrypted", response.Passport.IssuingAuthority);
        Assert.Equal("2.issueDate|encrypted", response.Passport.IssueDate);
        Assert.Equal("2.expirationDate|encrypted", response.Passport.ExpirationDate);
    }

    [Fact]
    public void Constructor_Passport_WithMinimalData_DeserializesSuccessfully()
    {
        var passportData = new CipherPassportData
        {
            Name = "2.name|encrypted",
            PassportNumber = "2.passportNumber|encrypted",
        };

        var cipher = new Cipher
        {
            Id = Guid.NewGuid(),
            Type = CipherType.Passport,
            Data = JsonSerializer.Serialize(passportData),
            RevisionDate = DateTime.UtcNow,
            CreationDate = DateTime.UtcNow,
        };

        var response = new FullCipherMiniResponseModel(FullCipherAccess.Unrestricted(), cipher, _globalSettings, false);

        Assert.Equal(CipherType.Passport, response.Type);
        Assert.NotNull(response.Passport);
        Assert.Equal("2.passportNumber|encrypted", response.Passport.PassportNumber);
        Assert.Null(response.Passport.Surname);
        Assert.Null(response.Passport.GivenName);
        Assert.Null(response.Passport.DateOfBirth);
    }

    [Fact]
    public void Constructor_DriversLicense_WithCustomFields_IncludesFields()
    {
        var driversLicenseData = new CipherDriversLicenseData
        {
            Name = "2.name|encrypted",
            LicenseNumber = "2.licenseNumber|encrypted",
            Fields = new List<CipherFieldData>
            {
                new CipherFieldData { Name = "2.fieldName|encrypted", Value = "2.fieldValue|encrypted", Type = FieldType.Text }
            }
        };

        var cipher = new Cipher
        {
            Id = Guid.NewGuid(),
            Type = CipherType.DriversLicense,
            Data = JsonSerializer.Serialize(driversLicenseData),
            RevisionDate = DateTime.UtcNow,
            CreationDate = DateTime.UtcNow,
        };

        var response = new FullCipherMiniResponseModel(FullCipherAccess.Unrestricted(), cipher, _globalSettings, false);

        Assert.NotNull(response.Fields);
        Assert.Single(response.Fields);
        Assert.Equal("2.fieldName|encrypted", response.Fields.First().Name);
    }

    [Fact]
    public void Constructor_Passport_WithCustomFields_IncludesFields()
    {
        var passportData = new CipherPassportData
        {
            Name = "2.name|encrypted",
            PassportNumber = "2.passportNumber|encrypted",
            Fields = new List<CipherFieldData>
            {
                new CipherFieldData { Name = "2.fieldName|encrypted", Value = "2.fieldValue|encrypted", Type = FieldType.Text }
            }
        };

        var cipher = new Cipher
        {
            Id = Guid.NewGuid(),
            Type = CipherType.Passport,
            Data = JsonSerializer.Serialize(passportData),
            RevisionDate = DateTime.UtcNow,
            CreationDate = DateTime.UtcNow,
        };

        var response = new FullCipherMiniResponseModel(FullCipherAccess.Unrestricted(), cipher, _globalSettings, false);

        Assert.NotNull(response.Fields);
        Assert.Single(response.Fields);
        Assert.Equal("2.fieldName|encrypted", response.Fields.First().Name);
    }

    [Fact]
    public void Constructor_DriversLicense_PreservesRawDataField()
    {
        var driversLicenseData = new CipherDriversLicenseData
        {
            Name = "2.name|encrypted",
            LicenseNumber = "2.licenseNumber|encrypted",
        };

        var serializedData = JsonSerializer.Serialize(driversLicenseData);
        var cipher = new Cipher
        {
            Id = Guid.NewGuid(),
            Type = CipherType.DriversLicense,
            Data = serializedData,
            RevisionDate = DateTime.UtcNow,
            CreationDate = DateTime.UtcNow,
        };

        var response = new FullCipherMiniResponseModel(FullCipherAccess.Unrestricted(), cipher, _globalSettings, false);

        Assert.Equal(serializedData, response.Data);
    }

    [Fact]
    public void Constructor_Passport_PreservesRawDataField()
    {
        var passportData = new CipherPassportData
        {
            Name = "2.name|encrypted",
            PassportNumber = "2.passportNumber|encrypted",
        };

        var serializedData = JsonSerializer.Serialize(passportData);
        var cipher = new Cipher
        {
            Id = Guid.NewGuid(),
            Type = CipherType.Passport,
            Data = serializedData,
            RevisionDate = DateTime.UtcNow,
            CreationDate = DateTime.UtcNow,
        };

        var response = new FullCipherMiniResponseModel(FullCipherAccess.Unrestricted(), cipher, _globalSettings, false);

        Assert.Equal(serializedData, response.Data);
    }

    [Fact]
    public void Constructor_Partial_Login_KeepsNameAndUrisAndStripsSecrets()
    {
        var loginData = new CipherLoginData
        {
            Name = "2.name|encrypted",
            Notes = "2.notes|encrypted",
            Username = "2.username|encrypted",
            Password = "2.password|encrypted",
            Totp = "2.totp|encrypted",
            Uris = new[]
            {
                new CipherLoginData.CipherLoginUriData { Uri = "2.uri|encrypted", UriChecksum = "2.checksum|encrypted" },
            },
        };

        var cipher = new Cipher
        {
            Id = Guid.NewGuid(),
            Type = CipherType.Login,
            Data = JsonSerializer.Serialize(loginData),
            RevisionDate = DateTime.UtcNow,
            CreationDate = DateTime.UtcNow,
        };

        var response = new CipherMiniResponseModel(cipher, _globalSettings, false);

        // Full data is withheld; the reduced blob is returned only in the separate PartialData field.
        Assert.Null(response.Data);
        Assert.NotNull(response.PartialData);
        Assert.Contains("2.name|encrypted", response.PartialData);
        Assert.Contains("2.uri|encrypted", response.PartialData);

        // The obsolete typed fields are withheld entirely — partial content lives only in PartialData.
        Assert.Null(response.Name);
        Assert.Null(response.Login);
        Assert.Null(response.Notes);

        // The reduced blob and the serialized model may not carry the secrets anywhere.
        Assert.DoesNotContain("username|encrypted", response.PartialData);
        Assert.DoesNotContain("password|encrypted", response.PartialData);
        Assert.DoesNotContain("totp|encrypted", response.PartialData);
        var serialized = JsonSerializer.Serialize(response);
        Assert.DoesNotContain("username|encrypted", serialized);
        Assert.DoesNotContain("password|encrypted", serialized);
        Assert.DoesNotContain("totp|encrypted", serialized);
    }

    [Fact]
    public void Constructor_Partial_NonLogin_KeepsOnlyName()
    {
        var cardData = new CipherCardData
        {
            Name = "2.name|encrypted",
            Notes = "2.notes|encrypted",
            Number = "2.number|encrypted",
            Code = "2.code|encrypted",
        };

        var cipher = new Cipher
        {
            Id = Guid.NewGuid(),
            Type = CipherType.Card,
            Data = JsonSerializer.Serialize(cardData),
            RevisionDate = DateTime.UtcNow,
            CreationDate = DateTime.UtcNow,
        };

        var response = new CipherMiniResponseModel(cipher, _globalSettings, false);

        Assert.Null(response.Data);
        Assert.NotNull(response.PartialData);
        Assert.Contains("2.name|encrypted", response.PartialData);
        // Obsolete typed fields withheld; partial content lives only in PartialData.
        Assert.Null(response.Name);
        Assert.Null(response.Card);
        Assert.Null(response.Notes);
        Assert.DoesNotContain("number|encrypted", response.PartialData);
        Assert.DoesNotContain("code|encrypted", response.PartialData);
    }

    [Fact]
    public void Constructor_Partial_BlobEncryptedData_WithholdsData()
    {
        const string opaque = "2.iv|ct|mac";
        var cipher = new Cipher
        {
            Id = Guid.NewGuid(),
            Type = CipherType.Login,
            Data = opaque,
            RevisionDate = DateTime.UtcNow,
            CreationDate = DateTime.UtcNow,
        };

        var response = new CipherMiniResponseModel(cipher, _globalSettings, false);

        // An opaque blob can't be reshaped, so neither full nor partial data is returned.
        Assert.Null(response.Data);
        Assert.Null(response.PartialData);
        Assert.Null(response.Login);
        Assert.Null(response.Name);
    }

    [Fact]
    public void Constructor_NotPartial_PreservesFullData()
    {
        var loginData = new CipherLoginData
        {
            Name = "2.name|encrypted",
            Username = "2.username|encrypted",
            Password = "2.password|encrypted",
        };

        var serializedData = JsonSerializer.Serialize(loginData);
        var cipher = new Cipher
        {
            Id = Guid.NewGuid(),
            Type = CipherType.Login,
            Data = serializedData,
            RevisionDate = DateTime.UtcNow,
            CreationDate = DateTime.UtcNow,
        };

        // The full-data path requires a gate-minted witness; the default-named type is partial only.
        var response = new FullCipherMiniResponseModel(FullCipherAccess.Unrestricted(), cipher, _globalSettings, false);

        Assert.Equal(serializedData, response.Data);
        Assert.Null(response.PartialData);
        Assert.Equal("2.username|encrypted", response.Login.Username);
        Assert.Equal("2.password|encrypted", response.Login.Password);
    }

    [Theory]
    [InlineData(CipherType.Login)]
    [InlineData(CipherType.SecureNote)]
    [InlineData(CipherType.Card)]
    [InlineData(CipherType.Identity)]
    [InlineData(CipherType.SSHKey)]
    [InlineData(CipherType.BankAccount)]
    [InlineData(CipherType.DriversLicense)]
    [InlineData(CipherType.Passport)]
    public void Constructor_OpaqueData_DoesNotThrowAndSkipsLegacyFields(CipherType type)
    {
        const string opaque = "2.iv|ct|mac";
        var cipher = new Cipher
        {
            Id = Guid.NewGuid(),
            Type = type,
            Data = opaque,
            RevisionDate = DateTime.UtcNow,
            CreationDate = DateTime.UtcNow,
        };

        var response = new FullCipherMiniResponseModel(FullCipherAccess.Unrestricted(), cipher, _globalSettings, false);

        Assert.Equal(type, response.Type);
        Assert.Equal(opaque, response.Data);
        Assert.Null(response.Name);
        Assert.Null(response.Notes);
        Assert.Null(response.Login);
        Assert.Null(response.SecureNote);
        Assert.Null(response.Card);
        Assert.Null(response.Identity);
        Assert.Null(response.SSHKey);
        Assert.Null(response.BankAccount);
        Assert.Null(response.DriversLicense);
        Assert.Null(response.Passport);
        Assert.Null(response.Fields);
        Assert.Null(response.PasswordHistory);
    }

    [Fact]
    public void Constructor_Partial_OmitsAttachments()
    {
        var cipher = new Cipher
        {
            Id = Guid.NewGuid(),
            Type = CipherType.Login,
            Data = JsonSerializer.Serialize(new CipherLoginData { Name = "2.name|encrypted" }),
            Attachments = JsonSerializer.Serialize(new Dictionary<string, CipherAttachment.MetaData>
            {
                ["attachment-id"] = new CipherAttachment.MetaData
                {
                    FileName = "2.file|encrypted",
                    Key = "2.attachmentKey|encrypted",
                    Size = 1024,
                },
            }),
            RevisionDate = DateTime.UtcNow,
            CreationDate = DateTime.UtcNow,
        };

        var response = new CipherMiniResponseModel(cipher, _globalSettings, false);

        // A leasing-gated (partial) response withholds attachment metadata entirely — including each
        // attachment's encryption Key — so nothing about the attachment leaks.
        Assert.Null(response.Attachments);
        var serialized = JsonSerializer.Serialize(response);
        Assert.DoesNotContain("2.attachmentKey|encrypted", serialized);
        Assert.DoesNotContain("2.file|encrypted", serialized);
    }

    [Fact]
    public void Constructor_NotPartial_IncludesAttachments()
    {
        var cipher = new Cipher
        {
            Id = Guid.NewGuid(),
            Type = CipherType.Login,
            Data = JsonSerializer.Serialize(new CipherLoginData { Name = "2.name|encrypted" }),
            Attachments = JsonSerializer.Serialize(new Dictionary<string, CipherAttachment.MetaData>
            {
                ["attachment-id"] = new CipherAttachment.MetaData
                {
                    FileName = "2.file|encrypted",
                    Key = "2.attachmentKey|encrypted",
                    Size = 1024,
                },
            }),
            RevisionDate = DateTime.UtcNow,
            CreationDate = DateTime.UtcNow,
        };

        var response = new FullCipherMiniResponseModel(FullCipherAccess.Unrestricted(), cipher, _globalSettings, false);

        // A full-access response still carries attachment metadata.
        Assert.NotNull(response.Attachments);
        var attachment = Assert.Single(response.Attachments);
        Assert.Equal("attachment-id", attachment.Id);
        Assert.Equal("2.attachmentKey|encrypted", attachment.Key);
    }
}
