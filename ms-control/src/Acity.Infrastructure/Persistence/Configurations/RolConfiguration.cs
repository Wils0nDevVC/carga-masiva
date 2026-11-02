using Acity.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Acity.Infrastructure.Persistence.Configurations;

public class RolConfiguration : IEntityTypeConfiguration<Rol>
{
    public void Configure(EntityTypeBuilder<Rol> builder)
    {
        builder.ToTable("Rol", "dbo");
        builder.HasKey(x => x.Id).HasName("PK_Rol");
        builder.Property(x => x.Id).HasColumnType("smallint").ValueGeneratedNever();
        builder.Property(x => x.Nombre).HasMaxLength(50).IsRequired();
        builder.HasIndex(x => x.Nombre).IsUnique().HasDatabaseName("UQ_Rol_Nombre");

        // Datos base aprobados (sin secretos)
        builder.HasData(
            new Rol { Id = 1, Nombre = "Administrador" },
            new Rol { Id = 2, Nombre = "Operador" });
    }
}
