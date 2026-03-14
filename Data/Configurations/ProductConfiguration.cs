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
        builder.Property(p => p.ImagenUrl).HasMaxLength(255);
        builder.Property(p => p.CreadoEn).HasDefaultValueSql("NOW()");

        // N:M con Category via ProductCategory
        builder.HasMany(p => p.ProductCategories)
               .WithOne(pc => pc.Product)
               .HasForeignKey(pc => pc.ProductId)
               .OnDelete(DeleteBehavior.Cascade);

        // N:M con Collection via ProductCollection
        builder.HasMany(p => p.ProductCollections)
               .WithOne(pc => pc.Product)
               .HasForeignKey(pc => pc.ProductId)
               .OnDelete(DeleteBehavior.Cascade);

        // N:M con CustomizationOption
        builder.HasMany(p => p.ProductCustomizationOptions)
               .WithOne(pco => pco.Product)
               .HasForeignKey(pco => pco.ProductId)
               .OnDelete(DeleteBehavior.Cascade);
    }
}
