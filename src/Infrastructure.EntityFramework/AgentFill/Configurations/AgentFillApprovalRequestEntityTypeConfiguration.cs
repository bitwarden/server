using Bit.Infrastructure.EntityFramework.AgentFill.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bit.Infrastructure.EntityFramework.AgentFill.Configurations;

public class AgentFillApprovalRequestEntityTypeConfiguration : IEntityTypeConfiguration<AgentFillApprovalRequest>
{
    public void Configure(EntityTypeBuilder<AgentFillApprovalRequest> builder)
    {
        builder
            .Property(e => e.Id)
            .ValueGeneratedNever();

        builder
            .HasOne(e => e.User)
            .WithMany()
            .HasForeignKey(e => e.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .HasOne(e => e.RequestDevice)
            .WithMany()
            .HasForeignKey(e => e.RequestDeviceId)
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .HasIndex(e => e.ExpirationDate)
            .IsClustered(false);

        builder.ToTable(nameof(AgentFillApprovalRequest));
    }
}
