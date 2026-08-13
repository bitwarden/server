using System.Text.Json;
using Bit.Api.Vault.Models;
using Bit.Api.Vault.Models.Request;
using Bit.Core.Vault.Entities;
using Bit.Core.Vault.Enums;
using Bit.Core.Vault.Models.Data;
using Xunit;

namespace Bit.Api.Test.Vault.Models.Request;

public class CipherRequestModelTests
{
    [Fact]
    public void ToCipher_DriversLicense_SerializesAllFields()
    {
        var request = new CipherRequestModel
        {
            Type = CipherType.DriversLicense,
            Name = "2.name|encrypted",
            Notes = "2.notes|encrypted",
            DriversLicense = new CipherDriversLicenseModel
            {
                FirstName = "2.firstName|encrypted",
                MiddleName = "2.middleName|encrypted",
                LastName = "2.lastName|encrypted",
                DateOfBirth = "2.dateOfBirth|encrypted",
                LicenseNumber = "2.licenseNumber|encrypted",
                IssuingCountry = "2.issuingCountry|encrypted",
                IssuingState = "2.issuingState|encrypted",
                IssueDate = "2.issueDate|encrypted",
                IssuingAuthority = "2.issuingAuthority|encrypted",
                ExpirationDate = "2.expirationDate|encrypted",
                LicenseClass = "2.licenseClass|encrypted",
            }
        };

        var cipher = new Cipher { Type = CipherType.DriversLicense };
        request.ToCipher(cipher);

        var data = JsonSerializer.Deserialize<CipherDriversLicenseData>(cipher.Data);

        Assert.NotNull(data);
        Assert.Equal("2.name|encrypted", data.Name);
        Assert.Equal("2.notes|encrypted", data.Notes);
        Assert.Equal("2.firstName|encrypted", data.FirstName);
        Assert.Equal("2.middleName|encrypted", data.MiddleName);
        Assert.Equal("2.lastName|encrypted", data.LastName);
        Assert.Equal("2.dateOfBirth|encrypted", data.DateOfBirth);
        Assert.Equal("2.licenseNumber|encrypted", data.LicenseNumber);
        Assert.Equal("2.issuingCountry|encrypted", data.IssuingCountry);
        Assert.Equal("2.issuingState|encrypted", data.IssuingState);
        Assert.Equal("2.issueDate|encrypted", data.IssueDate);
        Assert.Equal("2.issuingAuthority|encrypted", data.IssuingAuthority);
        Assert.Equal("2.expirationDate|encrypted", data.ExpirationDate);
        Assert.Equal("2.licenseClass|encrypted", data.LicenseClass);
    }

    [Fact]
    public void ToCipher_DriversLicense_WithNullFields_SerializesSuccessfully()
    {
        var request = new CipherRequestModel
        {
            Type = CipherType.DriversLicense,
            Name = "2.name|encrypted",
            DriversLicense = new CipherDriversLicenseModel
            {
                LicenseNumber = "2.licenseNumber|encrypted",
                // All other fields are null
            }
        };

        var cipher = new Cipher { Type = CipherType.DriversLicense };
        request.ToCipher(cipher);

        var data = JsonSerializer.Deserialize<CipherDriversLicenseData>(cipher.Data);

        Assert.NotNull(data);
        Assert.Equal("2.name|encrypted", data.Name);
        Assert.Equal("2.licenseNumber|encrypted", data.LicenseNumber);
        Assert.Null(data.FirstName);
        Assert.Null(data.MiddleName);
        Assert.Null(data.LastName);
        Assert.Null(data.DateOfBirth);
        Assert.Null(data.IssuingCountry);
        Assert.Null(data.IssuingState);
        Assert.Null(data.IssueDate);
        Assert.Null(data.IssuingAuthority);
        Assert.Null(data.ExpirationDate);
        Assert.Null(data.LicenseClass);
    }

    [Fact]
    public void ToCipher_DriversLicense_WithFields_IncludesCustomFields()
    {
        var request = new CipherRequestModel
        {
            Type = CipherType.DriversLicense,
            Name = "2.name|encrypted",
            DriversLicense = new CipherDriversLicenseModel(),
            Fields = new List<CipherFieldModel>
            {
                new CipherFieldModel { Name = "2.fieldName|encrypted", Value = "2.fieldValue|encrypted", Type = 0 }
            }
        };

        var cipher = new Cipher { Type = CipherType.DriversLicense };
        request.ToCipher(cipher);

        var data = JsonSerializer.Deserialize<CipherDriversLicenseData>(cipher.Data);

        Assert.NotNull(data);
        Assert.NotNull(data.Fields);
        Assert.Single(data.Fields);
    }

    [Fact]
    public void ToCipher_Passport_SerializesAllFields()
    {
        var request = new CipherRequestModel
        {
            Type = CipherType.Passport,
            Name = "2.name|encrypted",
            Notes = "2.notes|encrypted",
            Passport = new CipherPassportModel
            {
                Surname = "2.surname|encrypted",
                GivenName = "2.givenName|encrypted",
                DateOfBirth = "2.dateOfBirth|encrypted",
                Sex = "2.sex|encrypted",
                BirthPlace = "2.birthPlace|encrypted",
                Nationality = "2.nationality|encrypted",
                PassportNumber = "2.passportNumber|encrypted",
                PassportType = "2.passportType|encrypted",
                IssuingCountry = "2.issuingCountry|encrypted",
                IssuingAuthority = "2.issuingAuthority|encrypted",
                IssueDate = "2.issueDate|encrypted",
                ExpirationDate = "2.expirationDate|encrypted",
                NationalIdentificationNumber = "2.nationalIdentificationNumber|encrypted",
            }
        };

        var cipher = new Cipher { Type = CipherType.Passport };
        request.ToCipher(cipher);

        var data = JsonSerializer.Deserialize<CipherPassportData>(cipher.Data);

        Assert.NotNull(data);
        Assert.Equal("2.name|encrypted", data.Name);
        Assert.Equal("2.notes|encrypted", data.Notes);
        Assert.Equal("2.surname|encrypted", data.Surname);
        Assert.Equal("2.givenName|encrypted", data.GivenName);
        Assert.Equal("2.dateOfBirth|encrypted", data.DateOfBirth);
        Assert.Equal("2.sex|encrypted", data.Sex);
        Assert.Equal("2.birthPlace|encrypted", data.BirthPlace);
        Assert.Equal("2.nationality|encrypted", data.Nationality);
        Assert.Equal("2.passportNumber|encrypted", data.PassportNumber);
        Assert.Equal("2.passportType|encrypted", data.PassportType);
        Assert.Equal("2.issuingCountry|encrypted", data.IssuingCountry);
        Assert.Equal("2.issuingAuthority|encrypted", data.IssuingAuthority);
        Assert.Equal("2.issueDate|encrypted", data.IssueDate);
        Assert.Equal("2.expirationDate|encrypted", data.ExpirationDate);
        Assert.Equal("2.nationalIdentificationNumber|encrypted", data.NationalIdentificationNumber);
    }

    [Fact]
    public void ToCipher_Passport_WithNullFields_SerializesSuccessfully()
    {
        var request = new CipherRequestModel
        {
            Type = CipherType.Passport,
            Name = "2.name|encrypted",
            Passport = new CipherPassportModel
            {
                PassportNumber = "2.passportNumber|encrypted",
                // All other fields are null
            }
        };

        var cipher = new Cipher { Type = CipherType.Passport };
        request.ToCipher(cipher);

        var data = JsonSerializer.Deserialize<CipherPassportData>(cipher.Data);

        Assert.NotNull(data);
        Assert.Equal("2.name|encrypted", data.Name);
        Assert.Equal("2.passportNumber|encrypted", data.PassportNumber);
        Assert.Null(data.Surname);
        Assert.Null(data.GivenName);
        Assert.Null(data.DateOfBirth);
        Assert.Null(data.Sex);
        Assert.Null(data.BirthPlace);
        Assert.Null(data.Nationality);
        Assert.Null(data.PassportType);
        Assert.Null(data.IssuingCountry);
        Assert.Null(data.IssuingAuthority);
        Assert.Null(data.IssueDate);
        Assert.Null(data.ExpirationDate);
        Assert.Null(data.NationalIdentificationNumber);
    }

    [Fact]
    public void ToCipher_Passport_WithFields_IncludesCustomFields()
    {
        var request = new CipherRequestModel
        {
            Type = CipherType.Passport,
            Name = "2.name|encrypted",
            Passport = new CipherPassportModel(),
            Fields = new List<CipherFieldModel>
            {
                new CipherFieldModel { Name = "2.fieldName|encrypted", Value = "2.fieldValue|encrypted", Type = 0 }
            }
        };

        var cipher = new Cipher { Type = CipherType.Passport };
        request.ToCipher(cipher);

        var data = JsonSerializer.Deserialize<CipherPassportData>(cipher.Data);

        Assert.NotNull(data);
        Assert.NotNull(data.Fields);
        Assert.Single(data.Fields);
    }

    [Fact]
    public void ToCipher_DriversLicense_WithDataField_UsesDataDirectly()
    {
        var expectedData = "{\"Name\":\"2.name|encrypted\",\"LicenseNumber\":\"2.license|encrypted\"}";
        var request = new CipherRequestModel
        {
            Type = CipherType.DriversLicense,
            Name = "2.name|encrypted",
            Data = expectedData,
            DriversLicense = new CipherDriversLicenseModel
            {
                LicenseNumber = "2.different|encrypted" // Should be ignored when Data is provided
            }
        };

        var cipher = new Cipher { Type = CipherType.DriversLicense };
        request.ToCipher(cipher);

        Assert.Equal(expectedData, cipher.Data);
    }

    [Fact]
    public void ToCipher_Passport_WithDataField_UsesDataDirectly()
    {
        var expectedData = "{\"Name\":\"2.name|encrypted\",\"PassportNumber\":\"2.passport|encrypted\"}";
        var request = new CipherRequestModel
        {
            Type = CipherType.Passport,
            Name = "2.name|encrypted",
            Data = expectedData,
            Passport = new CipherPassportModel
            {
                PassportNumber = "2.different|encrypted" // Should be ignored when Data is provided
            }
        };

        var cipher = new Cipher { Type = CipherType.Passport };
        request.ToCipher(cipher);

        Assert.Equal(expectedData, cipher.Data);
    }
}
