using Bit.Core.Vault.Entities;
using Bit.Core.Vault.Enums;
using Bit.Seeder.Models;

namespace Bit.Seeder.Factories;

internal static class CardCipherSeeder
{
    internal static Cipher Create(
        string encryptionKey,
        string name,
        CardViewDto card,
        Guid? organizationId = null,
        Guid? userId = null,
        string? notes = null)
    {
        var cipherView = new CipherViewDto
        {
            OrganizationId = organizationId,
            Name = name,
            Notes = notes,
            Type = CipherTypes.Card,
            Card = card
        };

        var encrypted = CipherEncryption.Encrypt(cipherView, encryptionKey);
        return CipherEncryption.CreateEntity(encrypted, encrypted.ToCardData(), CipherType.Card, organizationId, userId);
    }

    internal static Cipher CreateFromSeed(
        string encryptionKey,
        SeedItem item,
        Guid? organizationId = null,
        Guid? userId = null)
    {
        var cipherView = new CipherViewDto
        {
            OrganizationId = organizationId,
            Name = item.Name,
            Notes = item.Notes,
            Type = CipherTypes.Card,
            Card = item.Card == null ? null : new CardViewDto
            {
                CardholderName = item.Card.CardholderName,
                Brand = item.Card.Brand,
                Number = item.Card.Number,
                ExpMonth = item.Card.ExpMonth,
                ExpYear = item.Card.ExpYear,
                Code = item.Card.Code
            },
            Fields = SeedItemMapping.MapFields(item.Fields)
        };

        var encrypted = CipherEncryption.Encrypt(cipherView, encryptionKey);
        return CipherEncryption.CreateEntity(encrypted, encrypted.ToCardData(), CipherType.Card, organizationId, userId);
    }
}
