using Acity.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Acity.Infrastructure.Persistence.Configurations;

public class ProductoConfiguration : IEntityTypeConfiguration<Producto>
{
    public void Configure(EntityTypeBuilder<Producto> builder)
    {
        builder.ToTable("Producto");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.CodigoProducto)
            .HasMaxLength(100)
            .UseCollation("Latin1_General_100_CI_AS_WS");
        builder.Property(e => e.NombreProducto).HasMaxLength(200);
        builder.Property(e => e.Precio).HasPrecision(18, 2);

        builder.HasOne<CargaPeriodo>().WithMany()
            .HasForeignKey(e => e.CargaPeriodoId)
            .OnDelete(DeleteBehavior.ClientSetNull);
    }
}
