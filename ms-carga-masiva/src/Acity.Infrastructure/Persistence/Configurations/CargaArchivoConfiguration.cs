using Acity.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Acity.Infrastructure.Persistence.Configurations;

public class CargaArchivoConfiguration : IEntityTypeConfiguration<CargaArchivo>
{
    public void Configure(EntityTypeBuilder<CargaArchivo> builder)
    {
        builder.ToTable("CargaArchivo");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.NombreArchivo).HasMaxLength(260);
        builder.Property(e => e.RutaArchivo).HasMaxLength(500);
        builder.Property(e => e.Estado)
            .HasMaxLength(20)
            .IsUnicode(false);

        builder.HasOne<Usuario>().WithMany()
            .HasForeignKey(e => e.UsuarioId)
            .OnDelete(DeleteBehavior.ClientSetNull);
    }
}
