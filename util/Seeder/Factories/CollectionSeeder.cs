using Bit.Core.Entities;
using Bit.RustSDK;

namespace Bit.Seeder.Factories;

public class CollectionSeeder(RustSdkService sdkService)
{
    public Collection CreateCollection(Guid organizationId, string orgKey, string name)
    {
        return new Collection
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            Name = sdkService.EncryptString(name, orgKey),
            CreationDate = DateTime.UtcNow,
            RevisionDate = DateTime.UtcNow
        };
    }

    public static CollectionUser CreateCollectionUser(
        Guid collectionId,
        Guid organizationUserId,
        bool readOnly = false,
        bool hidePasswords = false,
        bool manage = false)
    {
        return new CollectionUser
        {
            CollectionId = collectionId,
            OrganizationUserId = organizationUserId,
            ReadOnly = readOnly,
            HidePasswords = hidePasswords,
            Manage = manage
        };
    }
}
