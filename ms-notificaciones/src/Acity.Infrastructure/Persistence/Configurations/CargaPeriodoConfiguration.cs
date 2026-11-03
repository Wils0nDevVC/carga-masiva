using Acity.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Acity.Infrastructure.Persistence.Configurations;

/// <summary>Mapeo parcial de dbo.CargaPeriodo (solo lectura; Feature 016). El esquema lo crea el runner.</summary>
public class CargaPeriodoConfiguration : IEntityTypeConfiguration<CargaPeriodo>
{
    public void Configure(EntityTypeBuilder<CargaPeriodo> builder)
    {
        builder.ToTable("CargaPeriodo");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Periodo)
            .HasMaxLength(7)
            .IsUnicode(false)
            .IsFixedLength();
        builder.Property(e => e.Resultado)
            .HasMaxLength(20)
            .IsUnicode(false);

        builder.HasOne<CargaArchivo>().WithMany()
            .HasForeignKey(e => e.CargaId)
            .OnDelete(DeleteBehavior.ClientSetNull);
    }
}
