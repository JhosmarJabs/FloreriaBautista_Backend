using FloreriaBautista.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FloreriaBautista.Data.Configurations;

public class InventoryMovementConfiguration : IEntityTypeConfiguration<InventoryMovement>
{
    public void Configure(EntityTypeBuilder<InventoryMovement> builder)
    {
        builder.ToTable("inventory_movements");
        builder.HasKey(m => m.Id);
        builder.Property(m => m.TipoMovimiento).HasMaxLength(20).IsRequired();
        builder.Property(m => m.Motivo).HasMaxLength(255);
        builder.Property(m => m.FechaHora).HasDefaultValueSql("NOW()");
    }
}
