using FloreriaBautista.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FloreriaBautista.Data.Configurations;

public class CatalogoConfiguration : IEntityTypeConfiguration<Catalogo>
{
    public void Configure(EntityTypeBuilder<Catalogo> builder)
    {
        builder.ToTable("catalogos");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Nombre).HasMaxLength(100).IsRequired();
        builder.Property(c => c.Descripcion).HasMaxLength(255);
        builder.Property(c => c.Estado).HasMaxLength(20).HasDefaultValue("ACTIVA");
        builder.Property(c => c.ImagenUrl).HasMaxLength(255);
        builder.Property(c => c.CreadoEn).HasDefaultValueSql("NOW()");
        builder.Property(c => c.ActualizadoEn).HasDefaultValueSql("NOW()");
    }
}
