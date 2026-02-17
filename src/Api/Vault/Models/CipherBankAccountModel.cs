using Bit.Core.Utilities;
using Bit.Core.Vault.Models.Data;

namespace Bit.Api.Vault.Models;

public class CipherBankAccountModel
{
    public CipherBankAccountModel() { }

    public CipherBankAccountModel(CipherBankAccountData data)
    {
        BankName = data.BankName;
        NameOnAccount = data.NameOnAccount;
        AccountType = data.AccountType;
        AccountNumber = data.AccountNumber;
        RoutingNumber = data.RoutingNumber;
        BranchNumber = data.BranchNumber;
        Pin = data.Pin;
        SwiftCode = data.SwiftCode;
        Iban = data.Iban;
        BankContactPhone = data.BankContactPhone;
    }

    [EncryptedString]
    [EncryptedStringLength(1000)]
    public string? BankName { get; set; }

    [EncryptedString]
    [EncryptedStringLength(1000)]
    public string? NameOnAccount { get; set; }

    [EncryptedString]
    [EncryptedStringLength(1000)]
    public string? AccountType { get; set; }

    [EncryptedString]
    [EncryptedStringLength(1000)]
    public string? AccountNumber { get; set; }

    [EncryptedString]
    [EncryptedStringLength(1000)]
    public string? RoutingNumber { get; set; }

    [EncryptedString]
    [EncryptedStringLength(1000)]
    public string? BranchNumber { get; set; }

    [EncryptedString]
    [EncryptedStringLength(1000)]
    public string? Pin { get; set; }

    [EncryptedString]
    [EncryptedStringLength(1000)]
    public string? SwiftCode { get; set; }

    [EncryptedString]
    [EncryptedStringLength(1000)]
    public string? Iban { get; set; }

    [EncryptedString]
    [EncryptedStringLength(1000)]
    public string? BankContactPhone { get; set; }
}
