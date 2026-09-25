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
        builder.Property(i => i.Nombre).HasMaxLength(150).IsRequired();
        builder.Property(i => i.StockActual).HasDefaultValue(0);
        builder.Property(i => i.StockMinimo).HasDefaultValue(0);
        builder.Property(i => i.Sucursal).HasMaxLength(100).IsRequired();
        builder.Property(i => i.SumaAlCosto).HasDefaultValue(true);
        builder.Property(i => i.PrecioCosto)
               .HasColumnName("precio_costo")
               .HasColumnType("numeric(10,2)")
               .HasDefaultValue(0);

        builder.Property(i => i.EsFlorPrimaria)
               .HasColumnName("es_flor_primaria")
               .HasDefaultValue(false);
        builder.Property(i => i.UnidadMedida).HasMaxLength(20);
        builder.Property(i => i.ImagenUrl).HasColumnName("imagen_url").HasMaxLength(255);
        builder.Property(i => i.Activo).HasColumnName("activo").HasDefaultValue(true);
        builder.Property(i => i.ActualizadoEn).HasDefaultValueSql("NOW()");
        builder.HasIndex(i => i.ActualizadoEn);

        builder.Property(i => i.RendimientoEsperado)
               .HasColumnName("rendimiento_esperado")
               .HasColumnType("numeric(10,4)")
               .HasDefaultValue(1m);
        builder.Property(i => i.FactorMermaUso)
               .HasColumnName("factor_merma_uso")
               .HasColumnType("numeric(5,4)")
               .HasDefaultValue(0m);
        builder.Property(i => i.PrecioUnidadCompra)
               .HasColumnName("precio_unidad_compra")
               .HasColumnType("numeric(10,2)");
        builder.Property(i => i.UnidadCompra)
               .HasColumnName("unidad_compra")
               .HasMaxLength(20);
        builder.Property(i => i.VidaUtilDias)
               .HasColumnName("vida_util_dias");

        builder.HasMany(i => i.InventoryMovements)
               .WithOne(m => m.InventoryItem)
               .HasForeignKey(m => m.InventoryItemId);

        builder.HasMany(i => i.ProductRecipes)
               .WithOne(r => r.InventoryItem)
               .HasForeignKey(r => r.InventoryItemId)
               .OnDelete(DeleteBehavior.Restrict);
    }
}
