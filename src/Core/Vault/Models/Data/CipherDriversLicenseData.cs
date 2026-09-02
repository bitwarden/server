namespace Bit.Core.Vault.Models.Data;

public class CipherDriversLicenseData : CipherData
{
    public CipherDriversLicenseData() { }

    public string? FirstName { get; set; }
    public string? MiddleName { get; set; }
    public string? LastName { get; set; }
    public string? DateOfBirth { get; set; }
    public string? LicenseNumber { get; set; }
    public string? IssuingCountry { get; set; }
    public string? IssuingState { get; set; }
    public string? IssueDate { get; set; }
    public string? IssuingAuthority { get; set; }
    public string? ExpirationDate { get; set; }
    public string? LicenseClass { get; set; }
}
