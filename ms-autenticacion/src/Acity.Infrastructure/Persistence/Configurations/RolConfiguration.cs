using Acity.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Acity.Infrastructure.Persistence.Configurations;

public class RolConfiguration : IEntityTypeConfiguration<Rol>
{
    public void Configure(EntityTypeBuilder<Rol> builder)
    {
        builder.ToTable("Rol");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id).ValueGeneratedNever();
        builder.Property(e => e.Nombre).HasMaxLength(50);
    }
}
