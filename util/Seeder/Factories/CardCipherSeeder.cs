using Bit.Core.Vault.Entities;
using Bit.Core.Vault.Enums;
using Bit.Seeder.Models;

namespace Bit.Seeder.Factories;

internal static class CardCipherSeeder
{
    internal static Cipher Create(CipherSeed options)
    {

        var cipherView = new CipherViewDto
        {
            OrganizationId = options.OrganizationId,
            Name = options.Name,
            Notes = options.Notes,
            Type = CipherTypes.Card,
            Card = options.Card,
            Fields = options.Fields
        };

        var encrypted = CipherEncryption.Encrypt(cipherView, options.EncryptionKey!);
        return CipherEncryption.CreateEntity(encrypted, encrypted.ToCardData(), CipherType.Card, options.OrganizationId, options.UserId);
    }

}
