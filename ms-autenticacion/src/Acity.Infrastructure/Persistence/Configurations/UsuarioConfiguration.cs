using Acity.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Acity.Infrastructure.Persistence.Configurations;

public class UsuarioConfiguration : IEntityTypeConfiguration<Usuario>
{
    public void Configure(EntityTypeBuilder<Usuario> builder)
    {
        builder.ToTable("Usuario");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Email).HasMaxLength(256);
        builder.Property(e => e.PasswordHash)
            .HasMaxLength(255)
            .IsUnicode(false);

        builder.HasOne<Rol>().WithMany()
            .HasForeignKey(e => e.RolId)
            .OnDelete(DeleteBehavior.ClientSetNull);
    }
}
