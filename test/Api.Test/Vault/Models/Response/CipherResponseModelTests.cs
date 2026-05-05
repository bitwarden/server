using System.Text.Json;
using Bit.Api.Vault.Models.Response;
using Bit.Core.Settings;
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

        var response = new CipherMiniResponseModel(cipher, _globalSettings, false);

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

        var response = new CipherMiniResponseModel(cipher, _globalSettings, false);

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

        var response = new CipherMiniResponseModel(cipher, _globalSettings, false);

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

        var response = new CipherMiniResponseModel(cipher, _globalSettings, false);

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

        var response = new CipherMiniResponseModel(cipher, _globalSettings, false);

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

        var response = new CipherMiniResponseModel(cipher, _globalSettings, false);

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

        var response = new CipherMiniResponseModel(cipher, _globalSettings, false);

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

        var response = new CipherMiniResponseModel(cipher, _globalSettings, false);

        Assert.Equal(serializedData, response.Data);
    }
}
