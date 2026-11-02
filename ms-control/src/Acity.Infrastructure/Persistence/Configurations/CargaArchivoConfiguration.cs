using Acity.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Acity.Infrastructure.Persistence.Configurations;

public class CargaArchivoConfiguration : IEntityTypeConfiguration<CargaArchivo>
{
    public void Configure(EntityTypeBuilder<CargaArchivo> builder)
    {
        builder.ToTable("CargaArchivo", "dbo", t => t.HasCheckConstraint(
            "CK_CargaArchivo_Estado",
            "[Estado] IN ('Pendiente','En proceso','Cargado','Finalizado','Notificado','Fallido')"));
        builder.HasKey(x => x.Id).HasName("PK_CargaArchivo");
        builder.Property(x => x.Id).ValueGeneratedOnAdd();
        builder.Property(x => x.NombreArchivo).HasMaxLength(260).IsRequired();
        builder.Property(x => x.RutaArchivo).HasMaxLength(500).IsRequired();
        builder.Property(x => x.Estado).HasMaxLength(20).IsUnicode(false).IsRequired();
        builder.Property(x => x.FechaRecepcion).HasColumnType("datetime2");
        builder.Property(x => x.FechaFin).HasColumnType("datetime2");
        builder.Property(x => x.IntentosProceso).HasDefaultValue(0)
            .HasAnnotation(RelationalAnnotationNames.DefaultConstraintName, "DF_CargaArchivo_IntentosProceso");
        builder.HasOne<Usuario>().WithMany().HasForeignKey(x => x.UsuarioId)
            .HasConstraintName("FK_CargaArchivo_Usuario").OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.UsuarioId, x.FechaRecepcion }).HasDatabaseName("IX_CargaArchivo_Usuario_FechaRecepcion");
    }
}
