namespace Bit.Core.Vault.Models.Data;

public class CipherPassportData : CipherData
{
    public CipherPassportData() { }

    public string? Surname { get; set; }
    public string? GivenName { get; set; }
    public string? DateOfBirth { get; set; }
    public string? Sex { get; set; }
    public string? BirthPlace { get; set; }
    public string? Nationality { get; set; }
    public string? PassportNumber { get; set; }
    public string? PassportType { get; set; }
    public string? IssuingCountry { get; set; }
    public string? IssuingAuthority { get; set; }
    public string? IssueDate { get; set; }
    public string? ExpirationDate { get; set; }
    public string? NationalIdentificationNumber { get; set; }
}
