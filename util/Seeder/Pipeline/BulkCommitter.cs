using AutoMapper;
using Bit.Infrastructure.EntityFramework.Repositories;
using LinqToDB.Data;
using LinqToDB.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using EfCipher = Bit.Infrastructure.EntityFramework.Vault.Models.Cipher;
using EfCollection = Bit.Infrastructure.EntityFramework.AdminConsole.Models.Collection;
using EfCollectionGroup = Bit.Infrastructure.EntityFramework.AdminConsole.Models.CollectionGroup;
using EfCollectionUser = Bit.Infrastructure.EntityFramework.AdminConsole.Models.CollectionUser;
using EfFolder = Bit.Infrastructure.EntityFramework.Vault.Models.Folder;
using EfGroup = Bit.Infrastructure.EntityFramework.Models.Group;
using EfGroupUser = Bit.Infrastructure.EntityFramework.Models.GroupUser;
using EfOrganization = Bit.Infrastructure.EntityFramework.AdminConsole.Models.Organization;
using EfOrganizationApiKey = Bit.Infrastructure.EntityFramework.Models.OrganizationApiKey;
using EfOrganizationDomain = Bit.Infrastructure.EntityFramework.Models.OrganizationDomain;
using EfOrganizationUser = Bit.Infrastructure.EntityFramework.Models.OrganizationUser;
using EfSsoConfig = Bit.Infrastructure.EntityFramework.Auth.Models.SsoConfig;
using EfUser = Bit.Infrastructure.EntityFramework.Models.User;

namespace Bit.Seeder.Pipeline;

/// <summary>
/// Flushes accumulated entities from <see cref="SeederContext"/> to the database via BulkCopy.
/// </summary>
/// <remarks>
/// Entities are committed in foreign-key-safe order (Organizations → OrgApiKeys → Users → OrgUsers → … → Folders → Ciphers).
/// Most Core entities require AutoMapper conversion to their EF counterparts before insert;
/// a few (CollectionCipher) share the same type across layers and copy directly.
/// Cipher uses a seeder-internal <see cref="CipherBulkRow"/> projection — see <see cref="BulkInsertCiphers"/>.
/// Each list is cleared after insert so the context is ready for the next pipeline run.
///
/// CollectionUser and CollectionGroup require an explicit table name in BulkCopyOptions because
/// they lack .ToTable() mappings in DatabaseContext, so LinqToDB cannot resolve their table names
/// automatically. Table names vary by provider — SQL Server uses singular names while EF Core-managed
/// providers use pluralized names.
/// </remarks>
/// <seealso cref="SeederContext"/>
/// <seealso cref="RecipeExecutor"/>
internal sealed class BulkCommitter(DatabaseContext db, IMapper mapper)
{
    internal void Commit(SeederContext context)
    {
        MapCopyAndClear<Core.AdminConsole.Entities.Organization, EfOrganization>(context.Organizations);

        MapCopyAndClear<Core.Entities.OrganizationDomain, EfOrganizationDomain>(context.OrganizationDomains);

        MapAndCopy<Core.Entities.OrganizationApiKey, EfOrganizationApiKey>(context.OrganizationApiKey);

        CommitSsoConfigs(context.SsoConfigs);

        MapCopyAndClear<Core.Entities.User, EfUser>(context.Users);

        MapCopyAndClear<Core.Entities.OrganizationUser, EfOrganizationUser>(context.OrganizationUsers);

        MapCopyAndClear<Core.AdminConsole.Entities.Group, EfGroup>(context.Groups);

        MapCopyAndClear<Core.AdminConsole.Entities.GroupUser, EfGroupUser>(context.GroupUsers);

        MapCopyAndClear<Core.Entities.Collection, EfCollection>(context.Collections);

        MapCopyAndClear<Core.Entities.CollectionUser, EfCollectionUser>(context.CollectionUsers,
            GetTableName<EfCollectionUser>());

        MapCopyAndClear<Core.Entities.CollectionGroup, EfCollectionGroup>(context.CollectionGroups,
            GetTableName<EfCollectionGroup>());

        MapCopyAndClear<Core.Vault.Entities.Folder, EfFolder>(context.Folders);

        BulkInsertCiphers(context.Ciphers);

        CopyAndClear(context.CollectionCiphers);
    }

    /// <summary>
    /// Resolves the table name for an EF entity type from the EF Core model,
    /// falling back to the C# class name for SQL Server.
    /// </summary>
    private string? GetTableName<TEf>() where TEf : class =>
        db.Database.IsSqlServer()
            ? typeof(TEf).Name
            : db.Model.FindEntityType(typeof(TEf))?.GetTableName();

    private void MapCopyAndClear<TCore, TEf>(List<TCore> entities, string? tableName = null) where TEf : class
    {
        if (entities.Count is 0)
        {
            return;
        }

        var mapped = entities.Select(e => mapper.Map<TEf>(e));

        if (tableName is not null)
        {
            db.BulkCopy(new BulkCopyOptions { TableName = tableName }, mapped);
        }
        else
        {
            db.BulkCopy(mapped);
        }

        entities.Clear();
    }

    private void MapAndCopy<TCore, TEf>(TCore? entity) where TCore : class where TEf : class
    {
        if (entity is null)
        {
            return;
        }

        var mapped = mapper.Map<TEf>(entity);
        db.BulkCopy(new[] { mapped });
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

    /// <summary>
    /// Commits SsoConfig rows via EF Add/SaveChanges rather than BulkCopy: <c>SsoConfig.Id</c> is a
    /// database identity (long) that the EF change tracker populates on insert. The EF model inherits
    /// the Core entity, so only the settable columns are copied (CreationDate/RevisionDate default to UtcNow).
    /// Organizations are already flushed (BulkCopy above), so the FK is satisfied.
    /// </summary>
    private void CommitSsoConfigs(List<Core.Auth.Entities.SsoConfig> ssoConfigs)
    {
        if (ssoConfigs.Count is 0)
        {
            return;
        }

        foreach (var config in ssoConfigs)
        {
            db.SsoConfigs.Add(new EfSsoConfig
            {
                OrganizationId = config.OrganizationId,
                Enabled = config.Enabled,
                Data = config.Data,
            });
        }

        db.SaveChanges();
        ssoConfigs.Clear();
    }

    /// <summary>
    /// Bulk-inserts ciphers via a <see cref="CipherBulkRow"/> projection so the
    /// <c>Reprompt</c> column survives BulkCopy. LinqToDB's bulk path drops <c>CipherRepromptType?</c>
    /// on the EF model; projecting to <c>byte?</c> avoids the value-converter pipeline.
    /// Keep <see cref="CipherBulkRow"/> in sync with the <c>dbo.Cipher</c> schema.
    /// </summary>
    private void BulkInsertCiphers(List<Core.Vault.Entities.Cipher> ciphers)
    {
        if (ciphers.Count is 0)
        {
            return;
        }

        var tableName = GetTableName<EfCipher>();
        var rows = ciphers.Select(c => new CipherBulkRow
        {
            Id = c.Id,
            UserId = c.UserId,
            OrganizationId = c.OrganizationId,
            Type = (short)c.Type,
            Data = c.Data,
            Favorites = c.Favorites,
            Folders = c.Folders,
            Attachments = c.Attachments,
            CreationDate = c.CreationDate,
            RevisionDate = c.RevisionDate,
            DeletedDate = c.DeletedDate,
            Archives = c.Archives,
            Reprompt = c.Reprompt.HasValue ? (byte)c.Reprompt.Value : null,
            Key = c.Key,
        });

        var options = tableName is not null
            ? new BulkCopyOptions { TableName = tableName }
            : new BulkCopyOptions();

        db.BulkCopy(options, rows);
        ciphers.Clear();
    }

    /// <summary>
    /// Seeder-internal projection of <c>dbo.Cipher</c> with primitive column types.
    /// Property names match DB column names exactly so LinqToDB maps them by convention.
    /// </summary>
    private sealed class CipherBulkRow
    {
        public Guid Id { get; init; }

        public Guid? UserId { get; init; }

        public Guid? OrganizationId { get; init; }

        public short Type { get; init; }

        public string? Data { get; init; }

        public string? Favorites { get; init; }

        public string? Folders { get; init; }

        public string? Attachments { get; init; }

        public DateTime CreationDate { get; init; }

        public DateTime RevisionDate { get; init; }

        public DateTime? DeletedDate { get; init; }

        public string? Archives { get; init; }

        public byte? Reprompt { get; init; }

        public string? Key { get; init; }
    }
}
