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

    [Theory]
    [InlineData(CipherType.Login)]
    [InlineData(CipherType.SecureNote)]
    [InlineData(CipherType.Card)]
    [InlineData(CipherType.Identity)]
    [InlineData(CipherType.SSHKey)]
    [InlineData(CipherType.BankAccount)]
    [InlineData(CipherType.DriversLicense)]
    [InlineData(CipherType.Passport)]
    public void Constructor_BlobEncryptedData_DoesNotThrowAndSkipsLegacyFields(CipherType type)
    {
        const string blob = "{\"format_version\":1,\"wrapped_cek\":\"abc\",\"envelope\":\"def\"}";
        var cipher = new Cipher
        {
            Id = Guid.NewGuid(),
            Type = type,
            Data = blob,
            RevisionDate = DateTime.UtcNow,
            CreationDate = DateTime.UtcNow,
        };

        var response = new FullCipherMiniResponseModel(FullCipherAccess.Unrestricted(), cipher, _globalSettings, false);

        Assert.Equal(type, response.Type);
        Assert.Equal(blob, response.Data);
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

    private static Cipher LoginCipher(string data) => new()
    {
        Id = Guid.NewGuid(),
        Type = CipherType.Login,
        Data = data,
        RevisionDate = DateTime.UtcNow,
        CreationDate = DateTime.UtcNow,
    };

    [Fact]
    public void Constructor_Partial_Login_EmitsOnlyPartialDataAndWithholdsSecrets()
    {
        var cipher = LoginCipher(JsonSerializer.Serialize(new CipherLoginData
        {
            Name = "2.name|encrypted",
            Username = "2.username|encrypted",
            Password = "2.password|encrypted",
            Totp = "2.totp|encrypted",
            Notes = "2.notes|encrypted",
            Uris = [new CipherLoginData.CipherLoginUriData { Uri = "2.uri|encrypted" }],
        }));

        var response = new PartialCipherMiniResponseModel(cipher, false);

        Assert.Null(response.Data);
        Assert.NotNull(response.PartialData);
        Assert.Contains("2.name|encrypted", response.PartialData);
        Assert.Contains("2.uri|encrypted", response.PartialData);

        // The whole serialized model must be free of every withheld secret, not just the typed fields.
        var json = JsonSerializer.Serialize(response);
        Assert.DoesNotContain("2.username|encrypted", json);
        Assert.DoesNotContain("2.password|encrypted", json);
        Assert.DoesNotContain("2.totp|encrypted", json);
        Assert.DoesNotContain("2.notes|encrypted", json);

        // The obsolete typed fields are only populated on the witness-gated path.
        Assert.Null(response.Name);
        Assert.Null(response.Notes);
        Assert.Null(response.Login);
    }

    [Theory]
    [InlineData(CipherType.SecureNote)]
    [InlineData(CipherType.Card)]
    [InlineData(CipherType.Identity)]
    [InlineData(CipherType.SSHKey)]
    [InlineData(CipherType.BankAccount)]
    [InlineData(CipherType.DriversLicense)]
    [InlineData(CipherType.Passport)]
    public void Constructor_Partial_NonLogin_KeepsOnlyTheName(CipherType type)
    {
        var cipher = new Cipher
        {
            Id = Guid.NewGuid(),
            Type = type,
            Data = """{"Name":"2.name|encrypted","Notes":"2.notes|encrypted"}""",
            RevisionDate = DateTime.UtcNow,
            CreationDate = DateTime.UtcNow,
        };

        var response = new PartialCipherMiniResponseModel(cipher, false);

        Assert.Null(response.Data);
        Assert.Contains("2.name|encrypted", response.PartialData);
        Assert.DoesNotContain("2.notes|encrypted", response.PartialData);
    }

    [Fact]
    public void Constructor_Partial_BlobEncrypted_EmitsNeitherDataNorPartialData()
    {
        // An opaque SDK-encrypted blob can't be reshaped without decrypting, so nothing is returned.
        var cipher = LoginCipher("""{"format_version":1,"wrapped_cek":"abc","envelope":"def"}""");

        var response = new PartialCipherMiniResponseModel(cipher, false);

        Assert.Null(response.Data);
        Assert.Null(response.PartialData);
    }

    [Fact]
    public void Constructor_Partial_OmitsAttachments()
    {
        var cipher = LoginCipher(JsonSerializer.Serialize(new CipherLoginData { Name = "2.name|encrypted" }));
        cipher.Attachments = """{"id":{"Key":"2.attachmentkey|encrypted","FileName":"2.f|encrypted","Size":"1"}}""";

        var partial = new PartialCipherMiniResponseModel(cipher, false);
        var full = new FullCipherMiniResponseModel(FullCipherAccess.Unrestricted(), cipher, _globalSettings, false);

        // Attachment metadata carries each attachment's encryption key, so it is withheld too.
        Assert.Null(partial.Attachments);
        Assert.NotNull(full.Attachments);
    }

    [Fact]
    public void Constructor_Full_PreservesEverythingAndSetsNoPartialData()
    {
        var data = JsonSerializer.Serialize(new CipherLoginData
        {
            Name = "2.name|encrypted",
            Password = "2.password|encrypted",
        });
        var cipher = LoginCipher(data);

        var response = new FullCipherMiniResponseModel(FullCipherAccess.Unrestricted(), cipher, _globalSettings, false);

        Assert.Equal(data, response.Data);
        Assert.Null(response.PartialData);
        Assert.Equal("2.password|encrypted", response.Login.Password);
    }

    [Fact]
    public void Constructor_Full_WithoutAuthorizationForTheCipher_Throws()
    {
        var cipher = LoginCipher(JsonSerializer.Serialize(new CipherLoginData { Name = "2.name|encrypted" }));
        var accessForSomeoneElse = FullCipherAccess.ForCipher(Guid.NewGuid());

        // Fail closed: a witness that does not cover this cipher must not yield a full response.
        Assert.Throws<InvalidOperationException>(() =>
            new FullCipherMiniResponseModel(accessForSomeoneElse, cipher, _globalSettings, false));
    }
}

