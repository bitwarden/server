using Bit.Core.Entities;

namespace Bit.Seeder.Factories;

internal static class CollectionGroupSeeder
{
    internal static CollectionGroup Create(
        Guid collectionId,
        Guid groupId,
        bool readOnly = false,
        bool hidePasswords = false,
        bool manage = false)
    {
        return new CollectionGroup
        {
            CollectionId = collectionId,
            GroupId = groupId,
            ReadOnly = readOnly,
            HidePasswords = hidePasswords,
            Manage = manage
        };
    }
}
