using Bit.Core.Entities;

namespace Bit.Seeder.Factories;

internal static class CollectionUserSeeder
{
    internal static CollectionUser Create(
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
