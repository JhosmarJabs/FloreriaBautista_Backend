using FloreriaBautista.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FloreriaBautista.Data.Configurations;

public class AuthTokenConfiguration : IEntityTypeConfiguration<AuthToken>
{
    public void Configure(EntityTypeBuilder<AuthToken> builder)
    {
        builder.ToTable("auth_tokens");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Token).HasMaxLength(512).IsRequired();
        builder.Property(t => t.Tipo).HasMaxLength(30).IsRequired();
        builder.HasIndex(t => t.Token).IsUnique();
        builder.Property(t => t.CreadoEn).HasDefaultValueSql("NOW()");
    }
}
