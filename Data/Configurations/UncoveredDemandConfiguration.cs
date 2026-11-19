using FloreriaBautista.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FloreriaBautista.Data.Configurations;

public class UncoveredDemandConfiguration : IEntityTypeConfiguration<UncoveredDemand>
{
    public void Configure(EntityTypeBuilder<UncoveredDemand> builder)
    {
        builder.ToTable("uncovered_demands");
        builder.HasKey(d => d.Id);
        builder.Property(d => d.Id).HasDefaultValueSql("gen_random_uuid()");
        builder.Property(d => d.Origen).HasMaxLength(50).IsRequired();
        builder.Property(d => d.FechaRegistro).HasDefaultValueSql("NOW()");

        builder.HasIndex(d => new { d.InventoryItemId, d.FechaRegistro });

        builder.HasOne(d => d.InventoryItem)
               .WithMany()
               .HasForeignKey(d => d.InventoryItemId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(d => d.Product)
               .WithMany()
               .HasForeignKey(d => d.ProductId)
               .OnDelete(DeleteBehavior.SetNull);
    }
}
