using FloreriaBautista.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FloreriaBautista.Data.Configurations;

public class FlowerMovementConfiguration : IEntityTypeConfiguration<FlowerMovement>
{
    public void Configure(EntityTypeBuilder<FlowerMovement> builder)
    {
        builder.ToTable("flower_movements");
        builder.HasKey(m => m.Id);
        builder.Property(m => m.Tipo).HasMaxLength(20).IsRequired();
        builder.Property(m => m.Motivo).HasMaxLength(255);
        builder.Property(m => m.FechaHora).HasDefaultValueSql("NOW()");
    }
}
