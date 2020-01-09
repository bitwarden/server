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

        protected override void OnConfiguring(DbContextOptionsBuilder builder)
        {
            if(!string.IsNullOrWhiteSpace(_globalSettings.PostgreSql?.ConnectionString))
            {
                builder.UseNpgsql(_globalSettings.PostgreSql.ConnectionString);
            }
        }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            builder.Entity<User>().ToTable(nameof(User));
            builder.Entity<Cipher>().ToTable(nameof(Cipher));
        }
    }
}
