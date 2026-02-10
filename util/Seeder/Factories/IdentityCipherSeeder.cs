using Bit.Core.Vault.Entities;
using Bit.Core.Vault.Enums;
using Bit.Seeder.Models;

namespace Bit.Seeder.Factories;

internal static class IdentityCipherSeeder
{
    internal static Cipher Create(
        string encryptionKey,
        string name,
        IdentityViewDto identity,
        Guid? organizationId = null,
        Guid? userId = null,
        string? notes = null)
    {
        var cipherView = new CipherViewDto
        {
            OrganizationId = organizationId,
            Name = name,
            Notes = notes,
            Type = CipherTypes.Identity,
            Identity = identity
        };

        var encrypted = CipherEncryption.Encrypt(cipherView, encryptionKey);
        return CipherEncryption.CreateEntity(encrypted, encrypted.ToIdentityData(), CipherType.Identity, organizationId, userId);
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
            Type = CipherTypes.Identity,
            Identity = item.Identity == null ? null : new IdentityViewDto
            {
                FirstName = item.Identity.FirstName,
                MiddleName = item.Identity.MiddleName,
                LastName = item.Identity.LastName,
                Address1 = item.Identity.Address1,
                Address2 = item.Identity.Address2,
                Address3 = item.Identity.Address3,
                City = item.Identity.City,
                State = item.Identity.State,
                PostalCode = item.Identity.PostalCode,
                Country = item.Identity.Country,
                Company = item.Identity.Company,
                Email = item.Identity.Email,
                Phone = item.Identity.Phone,
                SSN = item.Identity.Ssn,
                Username = item.Identity.Username,
                PassportNumber = item.Identity.PassportNumber,
                LicenseNumber = item.Identity.LicenseNumber
            },
            Fields = SeedItemMapping.MapFields(item.Fields)
        };

        var encrypted = CipherEncryption.Encrypt(cipherView, encryptionKey);
        return CipherEncryption.CreateEntity(encrypted, encrypted.ToIdentityData(), CipherType.Identity, organizationId, userId);
    }
}
