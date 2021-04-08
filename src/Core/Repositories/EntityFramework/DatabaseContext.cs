using System;
using Bit.Core.Models.EntityFramework;
using Microsoft.EntityFrameworkCore;

namespace Bit.Core.Repositories.EntityFramework
{
    public class DatabaseContext : DbContext
    {
        private DbContextOptions<DatabaseContext> _options { get; set; }
        public DatabaseContext(DbContextOptions<DatabaseContext> options)
            : base(options)
        { 
            _options = options;
        }

        public DbSet<Cipher> Ciphers { get; set; }
        public DbSet<Collection> Collections { get; set; }
        public DbSet<CollectionCipher> CollectionCiphers { get; set; }
        public DbSet<Device> Devices { get; set; }
        public DbSet<EmergencyAccess> EmergencyAccesses { get; set; }
        public DbSet<Event> Events { get; set; }
        public DbSet<Folder> Folders { get; set; }
        public DbSet<Grant> Grants { get; set; }
        public DbSet<Group> Groups { get; set; }
        public DbSet<GroupUser> GroupUsers { get; set; }
        public DbSet<Installation> Installations { get; set; }
        public DbSet<Organization> Organizations { get; set; }
        public DbSet<OrganizationUser> OrganizationUsers { get; set; }
        public DbSet<Policy> Policies { get; set; }
        public DbSet<Send> Sends { get; set; }
        public DbSet<SsoConfig> SsoConfigs { get; set; }
        public DbSet<SsoUser> SsoUsers { get; set; }
        public DbSet<TaxRate> TaxRates { get; set; }
        public DbSet<Transaction> Transactions { get; set; }
        public DbSet<U2f> U2fs { get; set; }
        public DbSet<User> Users { get; set; }
        
        protected override void OnModelCreating(ModelBuilder builder)
        {
            var eCipher = builder.Entity<Cipher>();
            var eCollection = builder.Entity<Collection>();
            var eCollectionCipher = builder.Entity<CollectionCipher>();
            var eDevice = builder.Entity<Device>();
            var eEmergencyAccess = builder.Entity<EmergencyAccess>();
            var eEvent = builder.Entity<Event>();
            var eFolder = builder.Entity<Folder>();
            var eGrant = builder.Entity<Grant>();
            var eGroup = builder.Entity<Group>();
            var eGroupUser = builder.Entity<GroupUser>();
            var eInstallation = builder.Entity<Installation>();
            var eOrganization = builder.Entity<Organization>();
            var eOrganizationUser = builder.Entity<OrganizationUser>();
            var ePolicy = builder.Entity<Policy>();
            var eSend = builder.Entity<Send>();
            var eSsoConfig = builder.Entity<SsoConfig>();
            var eSsoUser = builder.Entity<SsoUser>();
            var eTaxRate = builder.Entity<TaxRate>();
            var eTransaction = builder.Entity<Transaction>();
            var eU2f = builder.Entity<U2f>();
            var eUser = builder.Entity<User>();

            eCipher.Property(e => e.AttachmentsJson).HasColumnName("Attachments");
            eCipher.Ignore(e => e.Attachments);
            eCipher.Property(e => e.DataJson).HasColumnName("Data");
            eCipher.Ignore(e => e.Data);
            eCipher.Property(e => e.FavoritesJson).HasColumnName("Favorites");
            eCipher.Ignore(e => e.Favorites);
            eCipher.Ignore(e => e.Folders);
            eCipher.Property(e => e.FoldersJson).HasColumnName("Folders");

            eCollectionCipher.HasNoKey();

            eGrant.HasNoKey();
            eCipher.Property(e => e.DataJson).HasColumnName("Data");
            eCipher.Ignore(e => e.Data);

            eGroupUser.HasNoKey();

            if (Database.IsNpgsql()) 
            {
                eOrganization.Property(e => e.TwoFactorProviders).HasColumnType("jsonb");
                eUser.Property(e => e.TwoFactorProviders).HasColumnType("jsonb");
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
            eOrganizationUser.ToTable(nameof(OrganizationUser));
            ePolicy.ToTable(nameof(Policy));
            eSend.ToTable(nameof(Send));
            eSsoConfig.ToTable(nameof(SsoConfig));
            eSsoUser.ToTable(nameof(SsoUser));
            eTaxRate.ToTable(nameof(TaxRate));
            eTransaction.ToTable(nameof(Transaction));
            eU2f.ToTable(nameof(U2f));
            eUser.ToTable(nameof(User));
        }
    }
}
