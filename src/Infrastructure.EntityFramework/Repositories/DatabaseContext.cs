using Bit.Infrastructure.EntityFramework.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Bit.Infrastructure.EntityFramework.Repositories;

public class DatabaseContext : DbContext
{
    public const string postgresIndetermanisticCollation = "postgresIndetermanisticCollation";

    public DatabaseContext(DbContextOptions<DatabaseContext> options)
        : base(options)
    { }

    public DbSet<Cipher> Ciphers { get; set; }
    public DbSet<Collection> Collections { get; set; }
    public DbSet<CollectionCipher> CollectionCiphers { get; set; }
    public DbSet<CollectionGroup> CollectionGroups { get; set; }
    public DbSet<CollectionUser> CollectionUsers { get; set; }
    public DbSet<Device> Devices { get; set; }
    public DbSet<EmergencyAccess> EmergencyAccesses { get; set; }
    public DbSet<Event> Events { get; set; }
    public DbSet<Folder> Folders { get; set; }
    public DbSet<Grant> Grants { get; set; }
    public DbSet<Group> Groups { get; set; }
    public DbSet<GroupUser> GroupUsers { get; set; }
    public DbSet<Installation> Installations { get; set; }
    public DbSet<Organization> Organizations { get; set; }
    public DbSet<OrganizationApiKey> OrganizationApiKeys { get; set; }
    public DbSet<OrganizationSponsorship> OrganizationSponsorships { get; set; }
    public DbSet<OrganizationConnection> OrganizationConnections { get; set; }
    public DbSet<OrganizationUser> OrganizationUsers { get; set; }
    public DbSet<Policy> Policies { get; set; }
    public DbSet<Provider> Providers { get; set; }
    public DbSet<ProviderUser> ProviderUsers { get; set; }
    public DbSet<ProviderOrganization> ProviderOrganizations { get; set; }
    public DbSet<Send> Sends { get; set; }
    public DbSet<SsoConfig> SsoConfigs { get; set; }
    public DbSet<SsoUser> SsoUsers { get; set; }
    public DbSet<TaxRate> TaxRates { get; set; }
    public DbSet<Transaction> Transactions { get; set; }
    public DbSet<User> Users { get; set; }
    public DbSet<AuthRequest> AuthRequests { get; set; }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        var eCipher = builder.Entity<Cipher>();
        var eCollection = builder.Entity<Collection>();
        var eCollectionCipher = builder.Entity<CollectionCipher>();
        var eCollectionUser = builder.Entity<CollectionUser>();
        var eCollectionGroup = builder.Entity<CollectionGroup>();
        var eDevice = builder.Entity<Device>();
        var eEmergencyAccess = builder.Entity<EmergencyAccess>();
        var eEvent = builder.Entity<Event>();
        var eFolder = builder.Entity<Folder>();
        var eGrant = builder.Entity<Grant>();
        var eGroup = builder.Entity<Group>();
        var eGroupUser = builder.Entity<GroupUser>();
        var eInstallation = builder.Entity<Installation>();
        var eOrganization = builder.Entity<Organization>();
        var eOrganizationSponsorship = builder.Entity<OrganizationSponsorship>();
        var eOrganizationUser = builder.Entity<OrganizationUser>();
        var ePolicy = builder.Entity<Policy>();
        var eProvider = builder.Entity<Provider>();
        var eProviderUser = builder.Entity<ProviderUser>();
        var eProviderOrganization = builder.Entity<ProviderOrganization>();
        var eSend = builder.Entity<Send>();
        var eSsoConfig = builder.Entity<SsoConfig>();
        var eSsoUser = builder.Entity<SsoUser>();
        var eTaxRate = builder.Entity<TaxRate>();
        var eTransaction = builder.Entity<Transaction>();
        var eUser = builder.Entity<User>();
        var eOrganizationApiKey = builder.Entity<OrganizationApiKey>();
        var eOrganizationConnection = builder.Entity<OrganizationConnection>();
        var eAuthRequest = builder.Entity<AuthRequest>();

        eCipher.Property(c => c.Id).ValueGeneratedNever();
        eCollection.Property(c => c.Id).ValueGeneratedNever();
        eEmergencyAccess.Property(c => c.Id).ValueGeneratedNever();
        eEvent.Property(c => c.Id).ValueGeneratedNever();
        eFolder.Property(c => c.Id).ValueGeneratedNever();
        eGroup.Property(c => c.Id).ValueGeneratedNever();
        eInstallation.Property(c => c.Id).ValueGeneratedNever();
        eOrganization.Property(c => c.Id).ValueGeneratedNever();
        eOrganizationSponsorship.Property(c => c.Id).ValueGeneratedNever();
        eOrganizationUser.Property(c => c.Id).ValueGeneratedNever();
        ePolicy.Property(c => c.Id).ValueGeneratedNever();
        eProvider.Property(c => c.Id).ValueGeneratedNever();
        eProviderUser.Property(c => c.Id).ValueGeneratedNever();
        eProviderOrganization.Property(c => c.Id).ValueGeneratedNever();
        eSend.Property(c => c.Id).ValueGeneratedNever();
        eTransaction.Property(c => c.Id).ValueGeneratedNever();
        eUser.Property(c => c.Id).ValueGeneratedNever();
        eOrganizationApiKey.Property(c => c.Id).ValueGeneratedNever();
        eOrganizationConnection.Property(c => c.Id).ValueGeneratedNever();
        eAuthRequest.Property(ar => ar.Id).ValueGeneratedNever();

        eCollectionCipher.HasKey(cc => new { cc.CollectionId, cc.CipherId });
        eCollectionUser.HasKey(cu => new { cu.CollectionId, cu.OrganizationUserId });
        eCollectionGroup.HasKey(cg => new { cg.CollectionId, cg.GroupId });
        eGrant.HasKey(x => x.Key);
        eGroupUser.HasKey(gu => new { gu.GroupId, gu.OrganizationUserId });


        if (Database.IsNpgsql())
        {
            // the postgres provider doesn't currently support database level non-deterministic collations.
            // see https://www.npgsql.org/efcore/misc/collations-and-case-sensitivity.html#database-collation
            builder.HasCollation(postgresIndetermanisticCollation, locale: "en-u-ks-primary", provider: "icu", deterministic: false);
            eUser.Property(e => e.Email).UseCollation(postgresIndetermanisticCollation);
            eSsoUser.Property(e => e.ExternalId).UseCollation(postgresIndetermanisticCollation);
            eOrganization.Property(e => e.Identifier).UseCollation(postgresIndetermanisticCollation);
            //
        }

        eCipher.ToTable(nameof(Cipher));
        eCollection.ToTable(nameof(Collection));
        eCollectionCipher.ToTable(nameof(CollectionCipher));
        eDevice.ToTable(nameof(Device));
        eEmergencyAccess.ToTable(nameof(EmergencyAccess));
        eEvent.ToTable(nameof(Event));
        eFolder.ToTable(nameof(Folder));
        eGrant.ToTable(nameof(Grant));
        eGroup.ToTable(nameof(Group));
        eGroupUser.ToTable(nameof(GroupUser));
        eInstallation.ToTable(nameof(Installation));
        eOrganization.ToTable(nameof(Organization));
        eOrganizationSponsorship.ToTable(nameof(OrganizationSponsorship));
        eOrganizationUser.ToTable(nameof(OrganizationUser));
        ePolicy.ToTable(nameof(Policy));
        eProvider.ToTable(nameof(Provider));
        eProviderUser.ToTable(nameof(ProviderUser));
        eProviderOrganization.ToTable(nameof(ProviderOrganization));
        eSend.ToTable(nameof(Send));
        eSsoConfig.ToTable(nameof(SsoConfig));
        eSsoUser.ToTable(nameof(SsoUser));
        eTaxRate.ToTable(nameof(TaxRate));
        eTransaction.ToTable(nameof(Transaction));
        eUser.ToTable(nameof(User));
        eOrganizationApiKey.ToTable(nameof(OrganizationApiKey));
        eOrganizationConnection.ToTable(nameof(OrganizationConnection));
        eAuthRequest.ToTable(nameof(AuthRequest));

        ConfigureDateTimeUTCQueries(builder);
    }

    // Make sure this is called after configuring all the entities as it iterates through all setup entities.
    private static void ConfigureDateTimeUTCQueries(ModelBuilder builder)
    {
        foreach (var entityType in builder.Model.GetEntityTypes())
        {
            if (entityType.IsKeyless)
            {
                continue;
            }
            foreach (var property in entityType.GetProperties())
            {
                if (property.ClrType == typeof(DateTime) || property.ClrType == typeof(DateTime?))
                {
                    property.SetValueConverter(
                        new ValueConverter<DateTime, DateTime>(
                            v => v,
                            v => new DateTime(v.Ticks, DateTimeKind.Utc)));
                }
            }
        }
    }
}
