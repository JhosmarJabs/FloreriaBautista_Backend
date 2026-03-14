using FloreriaBautista.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FloreriaBautista.Data.Configurations;

public class InventoryItemConfiguration : IEntityTypeConfiguration<InventoryItem>
{
    public void Configure(EntityTypeBuilder<InventoryItem> builder)
    {
        builder.ToTable("inventory_items");
        builder.HasKey(i => i.Id);
        builder.Property(i => i.Id).HasDefaultValueSql("gen_random_uuid()");
        builder.Property(i => i.Sucursal).HasMaxLength(100).IsRequired();
        builder.Property(i => i.StockActual).HasDefaultValue(0);
        builder.Property(i => i.StockMinimo).HasDefaultValue(0);

        // Un registro por producto/sucursal
        builder.HasIndex(i => new { i.ProductId, i.Sucursal }).IsUnique();

        builder.HasMany(i => i.InventoryMovements)
               .WithOne(m => m.InventoryItem)
               .HasForeignKey(m => m.InventoryItemId);
    }
}
