using Acity.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Acity.Infrastructure.Persistence.Configurations;

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
        builder.Property(e => e.PeriodoReservado)
            .HasMaxLength(7)
            .IsUnicode(false)
            .IsFixedLength();

        builder.HasOne<CargaArchivo>().WithMany()
            .HasForeignKey(e => e.CargaId)
            .OnDelete(DeleteBehavior.ClientSetNull);
    }
}
