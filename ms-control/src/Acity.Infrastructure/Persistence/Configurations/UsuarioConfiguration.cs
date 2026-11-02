using Acity.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Acity.Infrastructure.Persistence.Configurations;

public class UsuarioConfiguration : IEntityTypeConfiguration<Usuario>
{
    public void Configure(EntityTypeBuilder<Usuario> builder)
    {
        builder.ToTable("Usuario", "dbo");
        builder.HasKey(x => x.Id).HasName("PK_Usuario");
        builder.Property(x => x.Id).ValueGeneratedOnAdd();
        builder.Property(x => x.Email).HasMaxLength(256).IsRequired();
        builder.Property(x => x.PasswordHash).HasMaxLength(255).IsUnicode(false).IsRequired();
        builder.Property(x => x.RolId).HasColumnType("smallint");
        builder.HasIndex(x => x.Email).IsUnique().HasDatabaseName("UQ_Usuario_Email");
        builder.HasOne<Rol>().WithMany().HasForeignKey(x => x.RolId)
            .HasConstraintName("FK_Usuario_Rol").OnDelete(DeleteBehavior.Restrict);
    }
}
