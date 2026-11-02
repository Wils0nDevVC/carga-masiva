using Acity.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Acity.Infrastructure.Persistence.Configurations;

public class IncidenciaConfiguration : IEntityTypeConfiguration<Incidencia>
{
    public void Configure(EntityTypeBuilder<Incidencia> builder)
    {
        builder.ToTable("Incidencia");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Tipo)
            .HasMaxLength(30)
            .IsUnicode(false);
        builder.Property(e => e.CodigoProducto).HasMaxLength(100);
        builder.Property(e => e.PeriodoTexto).HasMaxLength(50);
        builder.Property(e => e.Motivo).HasMaxLength(500);
    }
}
