using Bit.Core;
using Bit.Infrastructure.EntityFramework.AdminConsole.Models;
using Bit.Infrastructure.EntityFramework.AdminConsole.Models.Provider;
using Bit.Infrastructure.EntityFramework.Auth.Models;
using Bit.Infrastructure.EntityFramework.Billing.Models;
using Bit.Infrastructure.EntityFramework.Converters;
using Bit.Infrastructure.EntityFramework.Models;
using Bit.Infrastructure.EntityFramework.NotificationCenter.Models;
using Bit.Infrastructure.EntityFramework.Platform;
using Bit.Infrastructure.EntityFramework.SecretsManager.Models;
using Bit.Infrastructure.EntityFramework.Tools.Models;
using Bit.Infrastructure.EntityFramework.Vault.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using DP = Microsoft.AspNetCore.DataProtection;

#nullable enable

namespace Bit.Infrastructure.EntityFramework.Repositories;

public class DatabaseContext : DbContext
{
    public const string postgresIndetermanisticCollation = "postgresIndetermanisticCollation";

    public DatabaseContext(DbContextOptions<DatabaseContext> options)
        : base(options)
    { }

    public DbSet<AccessPolicy> AccessPolicies { get; set; }
    public DbSet<UserProjectAccessPolicy> UserProjectAccessPolicy { get; set; }
    public DbSet<GroupProjectAccessPolicy> GroupProjectAccessPolicy { get; set; }
    public DbSet<ServiceAccountProjectAccessPolicy> ServiceAccountProjectAccessPolicy { get; set; }
    public DbSet<UserServiceAccountAccessPolicy> UserServiceAccountAccessPolicy { get; set; }
    public DbSet<GroupServiceAccountAccessPolicy> GroupServiceAccountAccessPolicy { get; set; }
    public DbSet<UserSecretAccessPolicy> UserSecretAccessPolicy { get; set; }
    public DbSet<GroupSecretAccessPolicy> GroupSecretAccessPolicy { get; set; }
    public DbSet<ServiceAccountSecretAccessPolicy> ServiceAccountSecretAccessPolicy { get; set; }
    public DbSet<ApiKey> ApiKeys { get; set; }
    public DbSet<Cache> Cache { get; set; }
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
    public DbSet<Secret> Secret { get; set; }
    public DbSet<ServiceAccount> ServiceAccount { get; set; }
    public DbSet<Project> Project { get; set; }
    public DbSet<ProviderUser> ProviderUsers { get; set; }
    public DbSet<ProviderOrganization> ProviderOrganizations { get; set; }
    public DbSet<Send> Sends { get; set; }
    public DbSet<SsoConfig> SsoConfigs { get; set; }
    public DbSet<SsoUser> SsoUsers { get; set; }
    public DbSet<TaxRate> TaxRates { get; set; }
    public DbSet<Transaction> Transactions { get; set; }
    public DbSet<User> Users { get; set; }
    public DbSet<AuthRequest> AuthRequests { get; set; }
    public DbSet<OrganizationDomain> OrganizationDomains { get; set; }
    public DbSet<WebAuthnCredential> WebAuthnCredentials { get; set; }
    public DbSet<ProviderPlan> ProviderPlans { get; set; }
    public DbSet<ProviderInvoiceItem> ProviderInvoiceItems { get; set; }
    public DbSet<Notification> Notifications { get; set; }
    public DbSet<NotificationStatus> NotificationStatuses { get; set; }
    public DbSet<ClientOrganizationMigrationRecord> ClientOrganizationMigrationRecords { get; set; }
    public DbSet<PasswordHealthReportApplication> PasswordHealthReportApplications { get; set; }
    public DbSet<SecurityTask> SecurityTasks { get; set; }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        // Scans and loads all configurations implementing the `IEntityTypeConfiguration` from the
        //  `Infrastructure.EntityFramework` Module. Note to get the assembly we can use a random class
        //   from this module.
        builder.ApplyConfigurationsFromAssembly(typeof(DatabaseContext).Assembly);

        // Going forward use `IEntityTypeConfiguration` in the Configurations folder for managing
        // Entity Framework code first database configurations.
        var eCipher = builder.Entity<Cipher>();
        var eCollection = builder.Entity<Collection>();
        var eCollectionCipher = builder.Entity<CollectionCipher>();
        var eCollectionUser = builder.Entity<CollectionUser>();
        var eCollectionGroup = builder.Entity<CollectionGroup>();
        var eEmergencyAccess = builder.Entity<EmergencyAccess>();
        var eFolder = builder.Entity<Folder>();
        var eGroup = builder.Entity<Group>();
        var eGroupUser = builder.Entity<GroupUser>();
        var eInstallation = builder.Entity<Installation>();
        var eProvider = builder.Entity<Provider>();
        var eProviderUser = builder.Entity<ProviderUser>();
        var eProviderOrganization = builder.Entity<ProviderOrganization>();
        var eSsoConfig = builder.Entity<SsoConfig>();
        var eTaxRate = builder.Entity<TaxRate>();
        var eUser = builder.Entity<User>();
        var eOrganizationApiKey = builder.Entity<OrganizationApiKey>();
        var eOrganizationConnection = builder.Entity<OrganizationConnection>();
        var eOrganizationDomain = builder.Entity<OrganizationDomain>();
        var aWebAuthnCredential = builder.Entity<WebAuthnCredential>();

        // Shadow property configurations go here

        eCipher.Property(c => c.Id).ValueGeneratedNever();
        eCollection.Property(c => c.Id).ValueGeneratedNever();
        eEmergencyAccess.Property(c => c.Id).ValueGeneratedNever();
        eFolder.Property(c => c.Id).ValueGeneratedNever();
        eGroup.Property(c => c.Id).ValueGeneratedNever();
        eInstallation.Property(c => c.Id).ValueGeneratedNever();
        eProvider.Property(c => c.Id).ValueGeneratedNever();
        eProviderUser.Property(c => c.Id).ValueGeneratedNever();
        eProviderOrganization.Property(c => c.Id).ValueGeneratedNever();
        eOrganizationApiKey.Property(c => c.Id).ValueGeneratedNever();
        eOrganizationConnection.Property(c => c.Id).ValueGeneratedNever();
        eOrganizationDomain.Property(ar => ar.Id).ValueGeneratedNever();
        aWebAuthnCredential.Property(ar => ar.Id).ValueGeneratedNever();

        eCollectionCipher.HasKey(cc => new { cc.CollectionId, cc.CipherId });
        eCollectionUser.HasKey(cu => new { cu.CollectionId, cu.OrganizationUserId });
        eCollectionGroup.HasKey(cg => new { cg.CollectionId, cg.GroupId });
        eGroupUser.HasKey(gu => new { gu.GroupId, gu.OrganizationUserId });

        var dataProtector = this.GetService<DP.IDataProtectionProvider>().CreateProtector(
            Constants.DatabaseFieldProtectorPurpose);
        var dataProtectionConverter = new DataProtectionConverter(dataProtector);
        eUser.Property(c => c.Key).HasConversion(dataProtectionConverter);
        eUser.Property(c => c.MasterPassword).HasConversion(dataProtectionConverter);

        if (Database.IsNpgsql())
        {
            // the postgres provider doesn't currently support database level non-deterministic collations.
            // see https://www.npgsql.org/efcore/misc/collations-and-case-sensitivity.html#database-collation
            builder.HasCollation(postgresIndetermanisticCollation, locale: "en-u-ks-primary", provider: "icu", deterministic: false);
            eUser.Property(e => e.Email).UseCollation(postgresIndetermanisticCollation);
            builder.Entity<Organization>().Property(e => e.Identifier).UseCollation(postgresIndetermanisticCollation);
            builder.Entity<SsoUser>().Property(e => e.ExternalId).UseCollation(postgresIndetermanisticCollation);
            //
        }

        eCipher.ToTable(nameof(Cipher));
        eCollection.ToTable(nameof(Collection));
        eCollectionCipher.ToTable(nameof(CollectionCipher));
        eEmergencyAccess.ToTable(nameof(EmergencyAccess));
        eFolder.ToTable(nameof(Folder));
        eGroup.ToTable(nameof(Group));
        eGroupUser.ToTable(nameof(GroupUser));
        eInstallation.ToTable(nameof(Installation));
        eProvider.ToTable(nameof(Provider));
        eProviderUser.ToTable(nameof(ProviderUser));
        eProviderOrganization.ToTable(nameof(ProviderOrganization));
        eSsoConfig.ToTable(nameof(SsoConfig));
        eTaxRate.ToTable(nameof(TaxRate));
        eOrganizationApiKey.ToTable(nameof(OrganizationApiKey));
        eOrganizationConnection.ToTable(nameof(OrganizationConnection));
        eOrganizationDomain.ToTable(nameof(OrganizationDomain));
        aWebAuthnCredential.ToTable(nameof(WebAuthnCredential));

        ConfigureDateTimeUtcQueries(builder);
    }

    // Make sure this is called after configuring all the entities as it iterates through all setup entities.
    private void ConfigureDateTimeUtcQueries(ModelBuilder builder)
    {
        ValueConverter<DateTime, DateTime> converter;
        if (Database.IsNpgsql())
        {
            converter = new ValueConverter<DateTime, DateTime>(
                v => v,
                d => new DateTimeOffset(d).UtcDateTime);
        }
        else
        {
            converter = new ValueConverter<DateTime, DateTime>(
                v => v,
                v => new DateTime(v.Ticks, DateTimeKind.Utc));
        }

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
                    property.SetValueConverter(converter);
                }
            }
        }
    }
}
