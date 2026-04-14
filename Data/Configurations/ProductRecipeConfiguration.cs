using FloreriaBautista.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FloreriaBautista.Data.Configurations;

public class ProductRecipeConfiguration : IEntityTypeConfiguration<ProductRecipe>
{
    public void Configure(EntityTypeBuilder<ProductRecipe> builder)
    {
        builder.ToTable("product_recipes");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).HasDefaultValueSql("gen_random_uuid()");
        builder.Property(r => r.CantidadRequerida).IsRequired();

        builder.HasIndex(r => new { r.ProductId, r.InventoryItemId }).IsUnique();

        builder.HasOne(r => r.Product)
               .WithMany(p => p.ProductRecipes)
               .HasForeignKey(r => r.ProductId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(r => r.InventoryItem)
               .WithMany(i => i.ProductRecipes)
               .HasForeignKey(r => r.InventoryItemId)
               .OnDelete(DeleteBehavior.Restrict);
    }
}
