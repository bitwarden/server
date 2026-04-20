using Bit.Core.Utilities;
using Bit.Core.Vault.Models.Data;

namespace Bit.Api.Vault.Models;

public class CipherDriversLicenseModel
{
    public CipherDriversLicenseModel() { }

    public CipherDriversLicenseModel(CipherDriversLicenseData data)
    {
        FirstName = data.FirstName;
        MiddleName = data.MiddleName;
        LastName = data.LastName;
        LicenseNumber = data.LicenseNumber;
        IssuingCountry = data.IssuingCountry;
        IssuingState = data.IssuingState;
        ExpirationDate = data.ExpirationDate;
        LicenseClass = data.LicenseClass;
    }

    [EncryptedString]
    [EncryptedStringLength(1000)]
    public string? FirstName { get; set; }

    [EncryptedString]
    [EncryptedStringLength(1000)]
    public string? MiddleName { get; set; }

    [EncryptedString]
    [EncryptedStringLength(1000)]
    public string? LastName { get; set; }

    [EncryptedString]
    [EncryptedStringLength(1000)]
    public string? LicenseNumber { get; set; }

    [EncryptedString]
    [EncryptedStringLength(1000)]
    public string? IssuingCountry { get; set; }

    [EncryptedString]
    [EncryptedStringLength(1000)]
    public string? IssuingState { get; set; }

    [EncryptedString]
    [EncryptedStringLength(1000)]
    public string? ExpirationDate { get; set; }

    [EncryptedString]
    [EncryptedStringLength(1000)]
    public string? LicenseClass { get; set; }
}
