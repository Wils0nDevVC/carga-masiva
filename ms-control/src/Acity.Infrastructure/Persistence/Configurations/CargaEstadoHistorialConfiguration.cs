using Acity.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Acity.Infrastructure.Persistence.Configurations;

public class CargaEstadoHistorialConfiguration : IEntityTypeConfiguration<CargaEstadoHistorial>
{
    public void Configure(EntityTypeBuilder<CargaEstadoHistorial> builder)
    {
        builder.ToTable("CargaEstadoHistorial", "dbo", t => t.HasCheckConstraint(
            "CK_CargaEstadoHistorial_Estado",
            "[Estado] IN ('Pendiente','En proceso','Cargado','Finalizado','Notificado','Fallido')"));
        builder.HasKey(x => x.Id).HasName("PK_CargaEstadoHistorial");
        builder.Property(x => x.Id).ValueGeneratedOnAdd();
        builder.Property(x => x.Estado).HasMaxLength(20).IsUnicode(false).IsRequired();
        builder.Property(x => x.FechaCambio).HasColumnType("datetime2");
        builder.HasOne<CargaArchivo>().WithMany().HasForeignKey(x => x.CargaId)
            .HasConstraintName("FK_CargaEstadoHistorial_Carga").OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.CargaId, x.Id }).HasDatabaseName("IX_CargaEstadoHistorial_Carga_Id");
    }
}
