using Bit.Infrastructure.EntityFramework.Models;
using Bit.Infrastructure.EntityFramework.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Bit.DbMigration;

/// <summary>
/// This is a proof of concept of what it might look like to have SQL Server switch over to being
/// an EF provider. 
/// </summary>
public class BitwardenVaultContext : DatabaseContext
{
    public BitwardenVaultContext(DbContextOptions<BitwardenVaultContext> options)
      : base(options)
    {

    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        if (Database.IsSqlServer())
        {
            builder.Entity<Device>(b =>
            {
                b.Property(d => d.Type)
                    .HasColumnType("smallint");
            });

            builder.Entity<EmergencyAccess>(b =>
            {
                b.Property(ea => ea.Type)
                    .HasColumnType("tinyint");

                b.Property(ea => ea.Status)
                    .HasColumnType("tinyint");

                b.Property(ea => ea.WaitTimeDays)
                    .HasColumnType("smallint");
            });

            builder.Entity<CollectionGroup>(b =>
            {
                // EF is naming these tables with an 's' on the end.
                // maybe we want to rename those tables eventually
                b.ToTable(nameof(CollectionGroup));
            });

            builder.Entity<CollectionUser>(b =>
            {
                // EF is naming these tables with an 's' on the end.
                // maybe we want to rename those tables eventually
                b.ToTable(nameof(CollectionUser));
            });
        }
    }
}
