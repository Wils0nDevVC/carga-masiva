using Acity.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Acity.Infrastructure.Persistence.Configurations;

public class CargaPeriodoConfiguration : IEntityTypeConfiguration<CargaPeriodo>
{
    public void Configure(EntityTypeBuilder<CargaPeriodo> builder)
    {
        builder.ToTable("CargaPeriodo", "dbo", t =>
        {
            t.HasCheckConstraint("CK_CargaPeriodo_Periodo",
                "[Periodo] LIKE '[0-9][0-9][0-9][0-9]-[0-9][0-9]' AND SUBSTRING([Periodo], 6, 2) BETWEEN '01' AND '12'");
            t.HasCheckConstraint("CK_CargaPeriodo_Resultado",
                "[Resultado] IN ('Admitido','Completado','Rechazado','Bloqueado','Fallido','NoProcesado')");
            t.HasCheckConstraint("CK_CargaPeriodo_Reserva",
                "([Resultado] IN ('Admitido','Completado') AND [PeriodoReservado] IS NOT NULL AND [PeriodoReservado] = [Periodo]) " +
                "OR ([Resultado] NOT IN ('Admitido','Completado') AND [PeriodoReservado] IS NULL)");
        });
        builder.HasKey(x => x.Id).HasName("PK_CargaPeriodo");
        builder.Property(x => x.Id).ValueGeneratedOnAdd();
        builder.Property(x => x.Periodo).HasMaxLength(7).IsFixedLength().IsUnicode(false).IsRequired();
        builder.Property(x => x.Resultado).HasMaxLength(20).IsUnicode(false).IsRequired();
        builder.Property(x => x.PeriodoReservado).HasMaxLength(7).IsFixedLength().IsUnicode(false);
        builder.Property(x => x.FechaResultado).HasColumnType("datetime2");
        builder.HasOne<CargaArchivo>().WithMany().HasForeignKey(x => x.CargaId)
            .HasConstraintName("FK_CargaPeriodo_Carga").OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.CargaId, x.Periodo }).IsUnique().HasDatabaseName("UQ_CargaPeriodo_Carga_Periodo");
        // Reserva: solo Admitido y Completado tienen valor; filtro con comparación simple (IS NOT NULL).
        builder.HasIndex(x => x.PeriodoReservado).IsUnique()
            .HasFilter("[PeriodoReservado] IS NOT NULL").HasDatabaseName("UX_CargaPeriodo_PeriodoReservado");
    }
}
