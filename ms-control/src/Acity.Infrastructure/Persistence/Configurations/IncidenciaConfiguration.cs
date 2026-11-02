using Acity.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Acity.Infrastructure.Persistence.Configurations;

public class IncidenciaConfiguration : IEntityTypeConfiguration<Incidencia>
{
    public void Configure(EntityTypeBuilder<Incidencia> builder)
    {
        builder.ToTable("Incidencia", "dbo", t => t.HasCheckConstraint(
            "CK_Incidencia_Tipo",
            "[Tipo] IN ('PeriodoRechazado','PeriodoBloqueado','FilaOmitida','Existente','NoProcesado','ErrorTecnico','ErrorNotificacion')"));
        builder.HasKey(x => x.Id).HasName("PK_Incidencia");
        builder.Property(x => x.Id).ValueGeneratedOnAdd();
        builder.Property(x => x.Tipo).HasMaxLength(30).IsUnicode(false).IsRequired();
        builder.Property(x => x.CodigoProducto).HasMaxLength(100);
        builder.Property(x => x.PeriodoTexto).HasMaxLength(50);
        builder.Property(x => x.Motivo).HasMaxLength(500).IsRequired();
        builder.Property(x => x.FechaRegistro).HasColumnType("datetime2");
        builder.HasOne<CargaArchivo>().WithMany().HasForeignKey(x => x.CargaId)
            .HasConstraintName("FK_Incidencia_Carga").OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<CargaPeriodo>().WithMany().HasForeignKey(x => x.CargaPeriodoId)
            .HasConstraintName("FK_Incidencia_CargaPeriodo").OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.CargaId, x.Id }).HasDatabaseName("IX_Incidencia_Carga_Id");
    }
}
