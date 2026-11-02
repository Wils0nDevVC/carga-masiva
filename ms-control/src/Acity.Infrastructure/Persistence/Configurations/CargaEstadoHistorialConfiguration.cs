using Acity.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Acity.Infrastructure.Persistence.Configurations;

public class CargaEstadoHistorialConfiguration : IEntityTypeConfiguration<CargaEstadoHistorial>
{
    public void Configure(EntityTypeBuilder<CargaEstadoHistorial> builder)
    {
        builder.ToTable("CargaEstadoHistorial");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Estado)
            .HasMaxLength(20)
            .IsUnicode(false);

        builder.HasOne<CargaArchivo>().WithMany()
            .HasForeignKey(e => e.CargaId)
            .OnDelete(DeleteBehavior.ClientSetNull);
    }
}
