using Bit.Core.Utilities;
using Bit.Core.Vault.Models.Data;

namespace Bit.Api.Vault.Models;

public class CipherPassportModel
{
    public CipherPassportModel() { }

    public CipherPassportModel(CipherPassportData data)
    {
        Surname = data.Surname;
        GivenName = data.GivenName;
        DateOfBirth = data.DateOfBirth;
        Sex = data.Sex;
        BirthPlace = data.BirthPlace;
        Nationality = data.Nationality;
        PassportNumber = data.PassportNumber;
        PassportType = data.PassportType;
        IssuingCountry = data.IssuingCountry;
        IssuingAuthority = data.IssuingAuthority;
        IssueDate = data.IssueDate;
        ExpirationDate = data.ExpirationDate;
        NationalIdentificationNumber = data.NationalIdentificationNumber;
    }

    [EncryptedString]
    [EncryptedStringLength(1000)]
    public string? Surname { get; set; }

    [EncryptedString]
    [EncryptedStringLength(1000)]
    public string? GivenName { get; set; }

    [EncryptedString]
    [EncryptedStringLength(1000)]
    public string? DateOfBirth { get; set; }

    [EncryptedString]
    [EncryptedStringLength(1000)]
    public string? Sex { get; set; }

    [EncryptedString]
    [EncryptedStringLength(1000)]
    public string? BirthPlace { get; set; }

    [EncryptedString]
    [EncryptedStringLength(1000)]
    public string? Nationality { get; set; }

    [EncryptedString]
    [EncryptedStringLength(1000)]
    public string? PassportNumber { get; set; }

    [EncryptedString]
    [EncryptedStringLength(1000)]
    public string? PassportType { get; set; }

    [EncryptedString]
    [EncryptedStringLength(1000)]
    public string? IssuingCountry { get; set; }

    [EncryptedString]
    [EncryptedStringLength(1000)]
    public string? IssuingAuthority { get; set; }

    [EncryptedString]
    [EncryptedStringLength(1000)]
    public string? IssueDate { get; set; }

    [EncryptedString]
    [EncryptedStringLength(1000)]
    public string? ExpirationDate { get; set; }

    [EncryptedString]
    [EncryptedStringLength(1000)]
    public string? NationalIdentificationNumber { get; set; }
}
