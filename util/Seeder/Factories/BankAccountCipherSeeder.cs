using Bit.Core.Vault.Entities;
using Bit.Core.Vault.Enums;
using Bit.Seeder.Models;

namespace Bit.Seeder.Factories;

internal static class BankAccountCipherSeeder
{
    internal static Cipher Create(
        string encryptionKey,
        string name,
        BankAccountViewDto bankAccount,
        Guid? organizationId = null,
        Guid? userId = null,
        string? notes = null)
    {
        var cipherView = new CipherViewDto
        {
            OrganizationId = organizationId,
            Name = name,
            Notes = notes,
            Type = CipherTypes.BankAccount,
            BankAccount = bankAccount
        };

        var encrypted = CipherEncryption.Encrypt(cipherView, encryptionKey);
        return CipherEncryption.CreateEntity(encrypted, encrypted.ToBankAccountData(), CipherType.BankAccount, organizationId, userId);
    }
}
