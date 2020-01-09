using System;
using Bit.Core.Models.EntityFramework;
using Microsoft.EntityFrameworkCore;

namespace Bit.Core.Repositories.EntityFramework
{
    public class DatabaseContext : DbContext
    {
        private readonly GlobalSettings _globalSettings;

        public DatabaseContext(
            DbContextOptions<DatabaseContext> options,
            GlobalSettings globalSettings)
            : base(options)
        {
            _globalSettings = globalSettings;
        }

        public DbSet<User> Users { get; set; }
        public DbSet<Cipher> Ciphers { get; set; }
        public DbSet<Organization> Organizations { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder builder)
        {
            if(!string.IsNullOrWhiteSpace(_globalSettings.PostgreSql?.ConnectionString))
            {
                builder.UseNpgsql(_globalSettings.PostgreSql.ConnectionString);
            }
        }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            builder.Entity<Cipher>().Ignore(e => e.Data);
            builder.Entity<Cipher>().Property(e => e.Data).HasColumnName("Data");
            builder.Entity<Cipher>().Ignore(e => e.Attachments);
            builder.Entity<Cipher>().Property(e => e.Attachments).HasColumnName("Attachments");

            builder.Entity<User>().Ignore(e => e.TwoFactorProviders);
            builder.Entity<User>().Property(e => e.TwoFactorProvidersJson).HasColumnName("TwoFactorProviders");

            builder.Entity<Organization>().Ignore(e => e.TwoFactorProviders);
            builder.Entity<Organization>().Property(e => e.TwoFactorProvidersJson).HasColumnName("TwoFactorProviders");

            builder.Entity<User>().ToTable(nameof(User));
            builder.Entity<Cipher>().ToTable(nameof(Cipher));
            builder.Entity<Organization>().ToTable(nameof(Organization));
        }
    }
}
