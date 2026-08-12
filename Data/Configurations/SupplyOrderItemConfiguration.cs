using FloreriaBautista.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FloreriaBautista.Data.Configurations;

public class SupplyOrderItemConfiguration : IEntityTypeConfiguration<SupplyOrderItem>
{
    public void Configure(EntityTypeBuilder<SupplyOrderItem> builder)
    {
        builder.ToTable("solicitud_reabastecimiento_item");
        builder.HasKey(i => i.Id);
        builder.Property(i => i.Id).HasDefaultValueSql("gen_random_uuid()");

        builder.Property(i => i.NombreSnapshot).HasMaxLength(150).IsRequired();
        builder.Property(i => i.UnidadMedida).HasMaxLength(30);
        builder.Property(i => i.EstadoLinea).HasMaxLength(20).HasDefaultValue("PENDIENTE").IsRequired();
        builder.Property(i => i.PrecioUnitario).HasColumnType("numeric(10,2)");
        builder.Property(i => i.Origen).HasMaxLength(120).HasDefaultValue("Manual").IsRequired();
        builder.Property(i => i.Observacion).HasMaxLength(255);

        // Un insumo no se repite dentro de la misma solicitud: la UI actualiza la cantidad
        // en vez de agregar una segunda línea, y así la recepción no se ambigua.
        builder.HasIndex(i => new { i.SupplyOrderId, i.InventoryItemId }).IsUnique();

        builder.HasOne(i => i.InventoryItem)
               .WithMany()
               .HasForeignKey(i => i.InventoryItemId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(i => i.InventoryMovement)
               .WithMany()
               .HasForeignKey(i => i.InventoryMovementId)
               .OnDelete(DeleteBehavior.SetNull);
    }
}
