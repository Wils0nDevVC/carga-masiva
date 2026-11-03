using Acity.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Acity.Infrastructure.Persistence.Configurations;

/// <summary>Mapeo parcial de dbo.CargaArchivo (solo lectura en Notificaciones): Id y Estado.</summary>
public class CargaArchivoConfiguration : IEntityTypeConfiguration<CargaArchivo>
{
    public void Configure(EntityTypeBuilder<CargaArchivo> builder)
    {
        builder.ToTable("CargaArchivo");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.NombreArchivo).HasMaxLength(260);
        builder.Property(e => e.Estado)
            .HasMaxLength(20)
            .IsUnicode(false);
    }
}
