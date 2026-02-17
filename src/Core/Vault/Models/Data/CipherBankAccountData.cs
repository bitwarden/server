namespace Bit.Core.Vault.Models.Data;

public class CipherBankAccountData : CipherData
{
    public CipherBankAccountData() { }

    public string? BankName { get; set; }
    public string? NameOnAccount { get; set; }
    public string? AccountType { get; set; }
    public string? AccountNumber { get; set; }
    public string? RoutingNumber { get; set; }
    public string? BranchNumber { get; set; }
    public string? Pin { get; set; }
    public string? SwiftCode { get; set; }
    public string? Iban { get; set; }
    public string? BankContactPhone { get; set; }
}
