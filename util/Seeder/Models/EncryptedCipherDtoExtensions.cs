using Bit.Core.Enums;
using Bit.Core.Vault.Enums;
using Bit.Core.Vault.Models.Data;

namespace Bit.Seeder.Models;

internal static class EncryptedCipherDtoExtensions
{
    internal static CipherLoginData ToLoginData(this EncryptedCipherDto e) => new()
    {
        Name = e.Name,
        Notes = e.Notes,
        Username = e.Login?.Username,
        Password = e.Login?.Password,
        Totp = e.Login?.Totp,
        PasswordRevisionDate = e.Login?.PasswordRevisionDate,
        Uris = e.Login?.Uris?.Select(u => new CipherLoginData.CipherLoginUriData
        {
            Uri = u.Uri,
            UriChecksum = u.UriChecksum,
            Match = u.Match.HasValue ? (UriMatchType?)u.Match : null
        }),
        Fields = e.ToFields()
    };

    internal static CipherCardData ToCardData(this EncryptedCipherDto e) => new()
    {
        Name = e.Name,
        Notes = e.Notes,
        CardholderName = e.Card?.CardholderName,
        Brand = e.Card?.Brand,
        Number = e.Card?.Number,
        ExpMonth = e.Card?.ExpMonth,
        ExpYear = e.Card?.ExpYear,
        Code = e.Card?.Code,
        Fields = e.ToFields()
    };

    internal static CipherIdentityData ToIdentityData(this EncryptedCipherDto e) => new()
    {
        Name = e.Name,
        Notes = e.Notes,
        Title = e.Identity?.Title,
        FirstName = e.Identity?.FirstName,
        MiddleName = e.Identity?.MiddleName,
        LastName = e.Identity?.LastName,
        Address1 = e.Identity?.Address1,
        Address2 = e.Identity?.Address2,
        Address3 = e.Identity?.Address3,
        City = e.Identity?.City,
        State = e.Identity?.State,
        PostalCode = e.Identity?.PostalCode,
        Country = e.Identity?.Country,
        Company = e.Identity?.Company,
        Email = e.Identity?.Email,
        Phone = e.Identity?.Phone,
        SSN = e.Identity?.SSN,
        Username = e.Identity?.Username,
        PassportNumber = e.Identity?.PassportNumber,
        LicenseNumber = e.Identity?.LicenseNumber,
        Fields = e.ToFields()
    };

    internal static CipherSecureNoteData ToSecureNoteData(this EncryptedCipherDto e) => new()
    {
        Name = e.Name,
        Notes = e.Notes,
        Type = (SecureNoteType)(e.SecureNote?.Type ?? 0),
        Fields = e.ToFields()
    };

    internal static CipherSSHKeyData ToSshKeyData(this EncryptedCipherDto e) => new()
    {
        Name = e.Name,
        Notes = e.Notes,
        PrivateKey = e.SshKey?.PrivateKey,
        PublicKey = e.SshKey?.PublicKey,
        KeyFingerprint = e.SshKey?.Fingerprint,
        Fields = e.ToFields()
    };

    internal static CipherBankAccountData ToBankAccountData(this EncryptedCipherDto e) => new()
    {
        Name = e.Name,
        Notes = e.Notes,
        BankName = e.BankAccount?.BankName,
        NameOnAccount = e.BankAccount?.NameOnAccount,
        AccountType = e.BankAccount?.AccountType,
        AccountNumber = e.BankAccount?.AccountNumber,
        RoutingNumber = e.BankAccount?.RoutingNumber,
        BranchNumber = e.BankAccount?.BranchNumber,
        Pin = e.BankAccount?.Pin,
        SwiftCode = e.BankAccount?.SwiftCode,
        Iban = e.BankAccount?.Iban,
        BankContactPhone = e.BankAccount?.BankContactPhone,
        Fields = e.ToFields()
    };

    private static IEnumerable<CipherFieldData>? ToFields(this EncryptedCipherDto e) =>
        e.Fields?.Select(f => new CipherFieldData
        {
            Name = f.Name,
            Value = f.Value,
            Type = (FieldType)f.Type,
            LinkedId = f.LinkedId
        });
}
