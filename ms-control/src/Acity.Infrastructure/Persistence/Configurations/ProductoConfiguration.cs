using Acity.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Acity.Infrastructure.Persistence.Configurations;

public class ProductoConfiguration : IEntityTypeConfiguration<Producto>
{
    public void Configure(EntityTypeBuilder<Producto> builder)
    {
        builder.ToTable("Producto", "dbo");
        builder.HasKey(x => x.Id).HasName("PK_Producto");
        builder.Property(x => x.Id).ValueGeneratedOnAdd();
        builder.Property(x => x.CodigoProducto).HasMaxLength(100).UseCollation("Latin1_General_100_CI_AS_WS").IsRequired();
        builder.Property(x => x.NombreProducto).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Precio).HasColumnType("decimal(18,2)");
        builder.HasOne<CargaPeriodo>().WithMany().HasForeignKey(x => x.CargaPeriodoId)
            .HasConstraintName("FK_Producto_CargaPeriodo").OnDelete(DeleteBehavior.Restrict);
        // Unicidad global, sin llave compuesta con el periodo.
        builder.HasIndex(x => x.CodigoProducto).IsUnique().HasDatabaseName("UX_Producto_CodigoProducto");
    }
}
