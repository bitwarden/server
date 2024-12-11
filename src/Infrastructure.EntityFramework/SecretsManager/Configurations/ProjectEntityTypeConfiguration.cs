﻿using Bit.Infrastructure.EntityFramework.SecretsManager.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bit.Infrastructure.EntityFramework.SecretsManager.Configurations;

public class ProjectEntityTypeConfiguration : IEntityTypeConfiguration<Project>
{
    public void Configure(EntityTypeBuilder<Project> builder)
    {
        builder.Property(s => s.Id).ValueGeneratedNever();

        builder.HasKey(s => s.Id).IsClustered();

        builder.HasIndex(s => s.DeletedDate).IsClustered(false);

        builder.HasIndex(s => s.OrganizationId).IsClustered(false);

        builder.ToTable(nameof(Project));
    }
}
