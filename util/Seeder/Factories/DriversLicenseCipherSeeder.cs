using Bit.Core.Vault.Entities;
using Bit.Core.Vault.Enums;
using Bit.Seeder.Models;

namespace Bit.Seeder.Factories;

internal static class DriversLicenseCipherSeeder
{
    internal static Cipher Create(
        string encryptionKey,
        string name,
        DriversLicenseViewDto driversLicense,
        Guid? organizationId = null,
        Guid? userId = null,
        string? notes = null)
    {
        var cipherView = new CipherViewDto
        {
            OrganizationId = organizationId,
            Name = name,
            Notes = notes,
            Type = CipherTypes.DriversLicense,
            DriversLicense = driversLicense
        };

        var encrypted = CipherEncryption.Encrypt(cipherView, encryptionKey);
        return CipherEncryption.CreateEntity(encrypted, encrypted.ToDriversLicenseData(), CipherType.DriversLicense, organizationId, userId);
    }
}
