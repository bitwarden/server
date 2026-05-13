using Bit.Api.Vault.Models;
using Bit.Core.Vault.Models.Data;
using Xunit;

namespace Bit.Api.Test.Vault.Models;

public class CipherDriversLicenseModelTests
{
    [Fact]
    public void Constructor_FromData_MapsAllFields()
    {
        var data = new CipherDriversLicenseData
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
        };

        var model = new CipherDriversLicenseModel(data);

        Assert.Equal(data.FirstName, model.FirstName);
        Assert.Equal(data.MiddleName, model.MiddleName);
        Assert.Equal(data.LastName, model.LastName);
        Assert.Equal(data.DateOfBirth, model.DateOfBirth);
        Assert.Equal(data.LicenseNumber, model.LicenseNumber);
        Assert.Equal(data.IssuingCountry, model.IssuingCountry);
        Assert.Equal(data.IssuingState, model.IssuingState);
        Assert.Equal(data.IssueDate, model.IssueDate);
        Assert.Equal(data.IssuingAuthority, model.IssuingAuthority);
        Assert.Equal(data.ExpirationDate, model.ExpirationDate);
        Assert.Equal(data.LicenseClass, model.LicenseClass);
    }

    [Fact]
    public void DefaultConstructor_AllFieldsNull()
    {
        var model = new CipherDriversLicenseModel();

        Assert.Null(model.FirstName);
        Assert.Null(model.MiddleName);
        Assert.Null(model.LastName);
        Assert.Null(model.DateOfBirth);
        Assert.Null(model.LicenseNumber);
        Assert.Null(model.IssuingCountry);
        Assert.Null(model.IssuingState);
        Assert.Null(model.IssueDate);
        Assert.Null(model.IssuingAuthority);
        Assert.Null(model.ExpirationDate);
        Assert.Null(model.LicenseClass);
    }
}
