using FloreriaBautista.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FloreriaBautista.Data.Configurations;

public class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.ToTable("products");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).HasDefaultValueSql("gen_random_uuid()");
        builder.Property(p => p.Nombre).HasMaxLength(150).IsRequired();
        builder.Property(p => p.Descripcion).IsRequired();
        builder.Property(p => p.PrecioBase).HasColumnType("numeric(10,2)").IsRequired();
        builder.Property(p => p.Tipo).HasMaxLength(50).IsRequired();
        builder.Property(p => p.Estado).HasMaxLength(20).HasDefaultValue("ACTIVO");
        builder.Property(p => p.Visibilidad).HasMaxLength(20).HasDefaultValue("AMBOS");
        builder.Property(p => p.ImagenUrl).HasMaxLength(255);
        builder.Property(p => p.CreadoEn).HasDefaultValueSql("NOW()");
        builder.Property(p => p.ActualizadoEn).HasDefaultValueSql("NOW()");

        builder.HasMany(p => p.ProductCategories)
               .WithOne(pc => pc.Product)
               .HasForeignKey(pc => pc.ProductId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(p => p.ProductCatalogos)
               .WithOne(pc => pc.Product)
               .HasForeignKey(pc => pc.ProductId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(p => p.ProductCustomizationOptions)
               .WithOne(pco => pco.Product)
               .HasForeignKey(pco => pco.ProductId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(p => p.ProductRecipes)
               .WithOne(r => r.Product)
               .HasForeignKey(r => r.ProductId)
               .OnDelete(DeleteBehavior.Cascade);
    }
}
