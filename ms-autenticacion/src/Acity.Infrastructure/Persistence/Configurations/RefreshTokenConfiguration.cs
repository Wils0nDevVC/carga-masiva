using Acity.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Acity.Infrastructure.Persistence.Configurations;

/// <summary>Mapeo de dbo.RefreshToken (V005). El esquema lo crea el runner, no EF.</summary>
public class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("RefreshToken");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.TokenHash)
            .HasMaxLength(32)
            .IsFixedLength();
        builder.HasIndex(e => e.TokenHash, "UQ_RefreshToken_TokenHash").IsUnique();

        builder.HasOne<Usuario>().WithMany()
            .HasForeignKey(e => e.UsuarioId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("FK_RefreshToken_Usuario");
    }
}
