﻿using Bit.Infrastructure.EntityFramework.Auth.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bit.Infrastructure.EntityFramework.Auth.Configurations;

public class AuthRequestConfiguration : IEntityTypeConfiguration<AuthRequest>
{
    public void Configure(EntityTypeBuilder<AuthRequest> builder)
    {
        builder.Property(ar => ar.Id).ValueGeneratedNever();

        builder.HasOne(a => a.OrganizationUser).WithMany()
            .HasForeignKey(p => new { p.OrganizationId, p.UserId })
            .HasPrincipalKey(p => new { p.OrganizationId, p.UserId })
            .IsRequired(false);

        builder.ToTable(nameof(AuthRequest));
    }
}
