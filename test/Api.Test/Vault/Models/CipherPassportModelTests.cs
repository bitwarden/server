using Bit.Api.Vault.Models;
using Bit.Core.Vault.Models.Data;
using Xunit;

namespace Bit.Api.Test.Vault.Models;

public class CipherPassportModelTests
{
    [Fact]
    public void Constructor_FromData_MapsAllFields()
    {
        var data = new CipherPassportData
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
        };

        var model = new CipherPassportModel(data);

        Assert.Equal(data.Surname, model.Surname);
        Assert.Equal(data.GivenName, model.GivenName);
        Assert.Equal(data.DateOfBirth, model.DateOfBirth);
        Assert.Equal(data.Sex, model.Sex);
        Assert.Equal(data.BirthPlace, model.BirthPlace);
        Assert.Equal(data.Nationality, model.Nationality);
        Assert.Equal(data.PassportNumber, model.PassportNumber);
        Assert.Equal(data.PassportType, model.PassportType);
        Assert.Equal(data.IssuingCountry, model.IssuingCountry);
        Assert.Equal(data.IssuingAuthority, model.IssuingAuthority);
        Assert.Equal(data.IssueDate, model.IssueDate);
        Assert.Equal(data.ExpirationDate, model.ExpirationDate);
        Assert.Equal(data.NationalIdentificationNumber, model.NationalIdentificationNumber);
    }

    [Fact]
    public void DefaultConstructor_AllFieldsNull()
    {
        var model = new CipherPassportModel();

        Assert.Null(model.Surname);
        Assert.Null(model.GivenName);
        Assert.Null(model.DateOfBirth);
        Assert.Null(model.Sex);
        Assert.Null(model.BirthPlace);
        Assert.Null(model.Nationality);
        Assert.Null(model.PassportNumber);
        Assert.Null(model.PassportType);
        Assert.Null(model.IssuingCountry);
        Assert.Null(model.IssuingAuthority);
        Assert.Null(model.IssueDate);
        Assert.Null(model.ExpirationDate);
        Assert.Null(model.NationalIdentificationNumber);
    }
}
