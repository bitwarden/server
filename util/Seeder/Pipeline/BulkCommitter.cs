using AutoMapper;
using Bit.Infrastructure.EntityFramework.Repositories;
using LinqToDB.EntityFrameworkCore;
using EfCollection = Bit.Infrastructure.EntityFramework.Models.Collection;
using EfCollectionGroup = Bit.Infrastructure.EntityFramework.Models.CollectionGroup;
using EfCollectionUser = Bit.Infrastructure.EntityFramework.Models.CollectionUser;
using EfGroup = Bit.Infrastructure.EntityFramework.Models.Group;
using EfGroupUser = Bit.Infrastructure.EntityFramework.Models.GroupUser;
using EfOrganization = Bit.Infrastructure.EntityFramework.AdminConsole.Models.Organization;
using EfOrganizationUser = Bit.Infrastructure.EntityFramework.Models.OrganizationUser;
using EfUser = Bit.Infrastructure.EntityFramework.Models.User;

namespace Bit.Seeder.Pipeline;

/// <summary>
/// Flushes accumulated entities from <see cref="SeederContext"/> to the database via BulkCopy.
/// </summary>
/// <remarks>
/// Entities are committed in foreign-key-safe order (Organizations → Users → OrgUsers → …).
/// Most Core entities require AutoMapper conversion to their EF counterparts before insert;
/// a few (Cipher, CollectionCipher) share the same type across layers and copy directly.
/// Each list is cleared after insert so the context is ready for the next pipeline run.
/// </remarks>
/// <seealso cref="SeederContext"/>
/// <seealso cref="RecipeExecutor"/>
internal sealed class BulkCommitter(DatabaseContext db, IMapper mapper)
{
    internal void Commit(SeederContext context)
    {
        MapCopyAndClear<Core.AdminConsole.Entities.Organization, EfOrganization>(context.Organizations);

        MapCopyAndClear<Core.Entities.User, EfUser>(context.Users);

        MapCopyAndClear<Core.Entities.OrganizationUser, EfOrganizationUser>(context.OrganizationUsers);

        MapCopyAndClear<Core.AdminConsole.Entities.Group, EfGroup>(context.Groups);

        MapCopyAndClear<Core.AdminConsole.Entities.GroupUser, EfGroupUser>(context.GroupUsers);

        MapCopyAndClear<Core.Entities.Collection, EfCollection>(context.Collections);

        MapCopyAndClear<Core.Entities.CollectionUser, EfCollectionUser>(context.CollectionUsers);

        MapCopyAndClear<Core.Entities.CollectionGroup, EfCollectionGroup>(context.CollectionGroups);

        CopyAndClear(context.Ciphers);

        CopyAndClear(context.CollectionCiphers);
    }

    private void MapCopyAndClear<TCore, TEf>(List<TCore> entities) where TEf : class
    {
        if (entities.Count is 0)
        {
            return;
        }

        db.BulkCopy(entities.Select(e => mapper.Map<TEf>(e)));
        entities.Clear();
    }

    private void CopyAndClear<T>(List<T> entities) where T : class
    {
        if (entities.Count is 0)
        {
            return;
        }

        db.BulkCopy(entities);
        entities.Clear();
    }
}
