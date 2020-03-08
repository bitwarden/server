using System;
using Bit.Core.Models.EntityFramework;
using Microsoft.EntityFrameworkCore;

namespace Bit.Core.Repositories.EntityFramework
{
    public class DatabaseContext : DbContext
    {
        public DatabaseContext(DbContextOptions<DatabaseContext> options)
            : base(options)
        { }

        public DbSet<User> Users { get; set; }
        public DbSet<Cipher> Ciphers { get; set; }
        public DbSet<Organization> Organizations { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            builder.Entity<Cipher>().Ignore(e => e.Data);
            builder.Entity<Cipher>().Property(e => e.DataJson).HasColumnName("Data");
            builder.Entity<Cipher>().Ignore(e => e.Attachments);
            builder.Entity<Cipher>().Property(e => e.AttachmentsJson).HasColumnName("Attachments");
            builder.Entity<Cipher>().Ignore(e => e.Favorites);
            builder.Entity<Cipher>().Property(e => e.FavoritesJson).HasColumnName("Favorites");
            builder.Entity<Cipher>().Ignore(e => e.Folders);
            builder.Entity<Cipher>().Property(e => e.FoldersJson).HasColumnName("Folders");

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
