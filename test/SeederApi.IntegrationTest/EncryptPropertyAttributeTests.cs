using Bit.Seeder.Attributes;
using Bit.Seeder.Models;
using Xunit;

namespace Bit.SeederApi.IntegrationTest;

public sealed class EncryptPropertyAttributeTests
{
    [Fact]
    public void GetFieldPaths_CipherViewDto_ReturnsCorrectCount()
    {
        var paths = EncryptPropertyAttribute.GetFieldPaths<CipherViewDto>();

        // 2 top-level (name, notes)
        // 3 login (username, password, totp)
        // 2 loginUri[*] (uri, uriChecksum)
        // 12 Fido2Credential (everything but discoverable)
        // 1 passwordHistory[*].password
        // 6 card
        // 18 identity
        // 3 sshKey
        // 10 bankAccount
        // 11 driversLicense
        // 13 passport
        // 2 fields[*] (name, value)
        Assert.Equal(83, paths.Length);
    }

    [Fact]
    public void GetFieldPaths_CipherViewDto_TopLevel()
    {
        var paths = ToSet<CipherViewDto>();

        Assert.Contains("name", paths);
        Assert.Contains("notes", paths);
    }

    [Fact]
    public void GetFieldPaths_CipherViewDto_Login()
    {
        var paths = ToSet<CipherViewDto>();

        Assert.Contains("login.username", paths);
        Assert.Contains("login.password", paths);
        Assert.Contains("login.totp", paths);
        Assert.Contains("login.uris[*].uri", paths);
        Assert.Contains("login.uris[*].uriChecksum", paths);
    }

    [Fact]
    public void GetFieldPaths_CipherViewDto_Card()
    {
        var paths = ToSet<CipherViewDto>();

        Assert.Contains("card.cardholderName", paths);
        Assert.Contains("card.brand", paths);
        Assert.Contains("card.number", paths);
        Assert.Contains("card.expMonth", paths);
        Assert.Contains("card.expYear", paths);
        Assert.Contains("card.code", paths);
    }

    [Fact]
    public void GetFieldPaths_CipherViewDto_Identity()
    {
        var paths = ToSet<CipherViewDto>();

        var expected = new[]
        {
            "identity.title", "identity.firstName", "identity.middleName", "identity.lastName",
            "identity.address1", "identity.address2", "identity.address3",
            "identity.city", "identity.state", "identity.postalCode", "identity.country",
            "identity.company", "identity.email", "identity.phone",
            "identity.ssn", "identity.username", "identity.passportNumber", "identity.licenseNumber"
        };

        foreach (var path in expected)
        {
            Assert.Contains(path, paths);
        }

        Assert.Equal(18, expected.Length);
    }

    [Fact]
    public void GetFieldPaths_CipherViewDto_SshKey()
    {
        var paths = ToSet<CipherViewDto>();

        Assert.Contains("sshKey.privateKey", paths);
        Assert.Contains("sshKey.publicKey", paths);
        Assert.Contains("sshKey.fingerprint", paths);
    }

    [Fact]
    public void GetFieldPaths_CipherViewDto_BankAccount()
    {
        var paths = ToSet<CipherViewDto>();

        var expected = new[]
        {
            "bankAccount.bankName", "bankAccount.nameOnAccount", "bankAccount.accountType",
            "bankAccount.accountNumber", "bankAccount.routingNumber", "bankAccount.branchNumber",
            "bankAccount.pin", "bankAccount.swiftCode", "bankAccount.iban", "bankAccount.bankContactPhone"
        };

        foreach (var path in expected)
        {
            Assert.Contains(path, paths);
        }

        Assert.Equal(10, expected.Length);
    }

    [Fact]
    public void GetFieldPaths_CipherViewDto_DriversLicense()
    {
        var paths = ToSet<CipherViewDto>();

        var expected = new[]
        {
            "driversLicense.firstName", "driversLicense.middleName", "driversLicense.lastName",
            "driversLicense.dateOfBirth", "driversLicense.licenseNumber", "driversLicense.issuingCountry",
            "driversLicense.issuingState", "driversLicense.issueDate", "driversLicense.issuingAuthority",
            "driversLicense.expirationDate", "driversLicense.licenseClass"
        };

        foreach (var path in expected)
        {
            Assert.Contains(path, paths);
        }

        Assert.Equal(11, expected.Length);
    }

    [Fact]
    public void GetFieldPaths_CipherViewDto_Passport()
    {
        var paths = ToSet<CipherViewDto>();

        var expected = new[]
        {
            "passport.surname", "passport.givenName", "passport.dateOfBirth", "passport.sex",
            "passport.birthPlace", "passport.nationality", "passport.passportNumber", "passport.passportType",
            "passport.issuingCountry", "passport.issuingAuthority", "passport.issueDate",
            "passport.expirationDate", "passport.nationalIdentificationNumber"
        };

        foreach (var path in expected)
        {
            Assert.Contains(path, paths);
        }

        Assert.Equal(13, expected.Length);
    }

    [Fact]
    public void GetFieldPaths_CipherViewDto_Fields()
    {
        var paths = ToSet<CipherViewDto>();

        Assert.Contains("fields[*].name", paths);
        Assert.Contains("fields[*].value", paths);
    }

    [Fact]
    public void GetFieldPaths_CipherViewDto_ExcludesNonEncryptedProperties()
    {
        var paths = ToSet<CipherViewDto>();

        Assert.DoesNotContain("id", paths);
        Assert.DoesNotContain("organizationId", paths);
        Assert.DoesNotContain("folderId", paths);
        Assert.DoesNotContain("key", paths);
        Assert.DoesNotContain("type", paths);
        Assert.DoesNotContain("favorite", paths);
        Assert.DoesNotContain("reprompt", paths);
        Assert.DoesNotContain("creationDate", paths);
        Assert.DoesNotContain("revisionDate", paths);
        Assert.DoesNotContain("deletedDate", paths);
    }

    [Fact]
    public void GetFieldPaths_CipherViewDto_ExcludesNonStringNestedProperties()
    {
        var paths = ToSet<CipherViewDto>();

        Assert.DoesNotContain("login.passwordRevisionDate", paths);
        Assert.DoesNotContain("login.uris[*].match", paths);
        Assert.DoesNotContain("fields[*].type", paths);
        Assert.DoesNotContain("fields[*].linkedId", paths);
        Assert.DoesNotContain("secureNote.type", paths);
    }

    [Fact]
    public void GetFieldPaths_UsesJsonPropertyName_NotCSharpPropertyName()
    {
        var paths = ToSet<CipherViewDto>();

        // C# property is "CardholderName", JSON path should use "cardholderName"
        Assert.Contains("card.cardholderName", paths);
        Assert.DoesNotContain("card.CardholderName", paths);

        // C# property is "SSN", JSON path should use "ssn"
        Assert.Contains("identity.ssn", paths);
        Assert.DoesNotContain("identity.SSN", paths);
    }

    [Fact]
    public void GetFieldPaths_IsCached()
    {
        var first = EncryptPropertyAttribute.GetFieldPaths<CipherViewDto>();
        var second = EncryptPropertyAttribute.GetFieldPaths<CipherViewDto>();

        Assert.Same(first, second);
    }

    [Fact]
    public void GetFieldPaths_TypeWithNoEncryptedProperties_ReturnsEmpty()
    {
        var paths = EncryptPropertyAttribute.GetFieldPaths<SecureNoteViewDto>();

        Assert.Empty(paths);
    }

    private static HashSet<string> ToSet<T>() =>
        new(EncryptPropertyAttribute.GetFieldPaths<T>());
}
