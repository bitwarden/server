using Bit.Infrastructure.EntityFramework.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Bit.Infrastructure.EntityFramework.Models;

namespace Bit.DbMigration;

public static class MigrateHandler
{
    // This will need to change if we add any new tables.

    private static readonly Type _migrator = typeof(TableMigrator<>);

    // Sorted by tables with 0 FK constraints in alphabetical order then by alphabetical order by what has had all its
    // FK constraints already migrated
    private static readonly List<Type> _migrateOrder = new()
    {
        // 0 FK Constraints
        typeof(Event),
        typeof(Grant),
        typeof(Installation),
        typeof(Organization),
        typeof(TaxRate),
        typeof(Provider),
        typeof(User),

        /// FK on <see cref="Organization" /> & <see cref="User" />
        typeof(Cipher),

        /// FK on <see cref="Organization" />
        typeof(Collection),

        /// FK on <see cref="Cipher" /> & <see cref="Collection" />
        typeof(CollectionCipher),

        /// FK on <see cref="User" />
        typeof(Device),

        /// FK on <see cref="Device" /> & <see cref="User" />
        typeof(AuthRequest),

        /// FK on <see cref="User" />
        typeof(EmergencyAccess),

        /// FK on <see cref="User" />
        typeof(Folder),

        /// FK on <see cref="Organization" />
        typeof(Group),

        /// FK on <see cref="Collection" /> & <see cref="Group" />
        typeof(CollectionGroup),

        /// FK on <see cref="Organization" />
        typeof(OrganizationApiKey),

        /// FK on <see cref="Organization" />
        typeof(OrganizationConnection),

        /// FK on <see cref="Organization" />
        typeof(OrganizationSponsorship),

        /// FK on <see cref="User" /> & <see cref="Organization" />
        typeof(OrganizationUser),

        /// FK on <see cref="Collection" /> & <see cref="OrganizationUser" />
        typeof(CollectionUser),

        /// FK on <see cref="Group" /> & <see cref="OrganizationUser" />
        typeof(GroupUser),

        /// FK on <see cref="Organization" />
        typeof(Policy),

        /// FK on <see cref="Organization" /> & <see cref="Provider" />
        typeof(ProviderOrganization),

        /// FK on <see cref="Provider" /> & <see cref="User" />
        typeof(ProviderUser),

        /// FK on <see cref="Organization" /> & <see cref="User" />
        typeof(Send),

        /// FK on <see cref="Organization" />
        typeof(SsoConfig),

        /// FK on <see cref="Organization" /> & <see cref="User" />
        typeof(SsoUser),

        /// FK on <see cref="Organization" /> & <see cref="User" />
        typeof(Transaction),
    };


    public static async Task RunAsync(ProviderOption fromProvider, string fromConnectionString, ToProviderOption toProvider, string toConnectionString)
    {
        var fromServiceProvider = Common.BuildContext(fromProvider, fromConnectionString);
        var fromContext = fromServiceProvider.GetRequiredService<BitwardenVaultContext>();

        // Lets do a little validation
        var fromEntities = fromContext.Model.GetEntityTypes();
        if (fromEntities.Count() != _migrateOrder.Count)
        {
            throw new Exception("From database seems a little off...");
        }

        var toServiceProvider = Common.BuildContext((ProviderOption)toProvider, toConnectionString);

        foreach (var entityType in _migrateOrder)
        {
            var migratorInstance = (ITableMigrator)Activator.CreateInstance(_migrator.MakeGenericType(entityType), fromContext, toServiceProvider.GetRequiredService<BitwardenVaultContext>())!;
            await migratorInstance.Migrate();
        }
    }

    private class TableMigrator<T> : ITableMigrator
        where T : class
    {
        public DbSet<T> FromSet { get; set; }
        public DatabaseContext ToContext { get; set; }

        public TableMigrator(BitwardenVaultContext fromDatabaseContext, BitwardenVaultContext toDatabaseContext)
        {
            FromSet = fromDatabaseContext.Set<T>();
            ToContext = toDatabaseContext;
        }

        public async Task Migrate()
        {
            var allItems = await FromSet.ToListAsync();
            await ToContext.Set<T>().AddRangeAsync(allItems);
            await ToContext.SaveChangesAsync();
        }
    }

    private interface ITableMigrator
    {
        Task Migrate();
    }
}
