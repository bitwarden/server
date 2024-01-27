using Bit.Infrastructure.EntityFramework.SecretsManager.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bit.Infrastructure.EntityFramework.SecretsManager.Configurations;

public class ProjectSecretEntityTypeConfiguration : IEntityTypeConfiguration<ProjectSecret>
{
    public void Configure(EntityTypeBuilder<ProjectSecret> builder)
    {
        builder.ToTable(nameof(ProjectSecret));
    }
}
