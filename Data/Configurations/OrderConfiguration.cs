using FloreriaBautista.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FloreriaBautista.Data.Configurations;

public class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.ToTable("orders");
        builder.HasKey(o => o.Id);
        builder.Property(o => o.Id).HasDefaultValueSql("gen_random_uuid()");
        builder.Property(o => o.TipoPedido).HasMaxLength(20).IsRequired();
        builder.Property(o => o.Canal).HasMaxLength(20).IsRequired();
        builder.Property(o => o.EstadoPedido).HasMaxLength(30).HasDefaultValue("PENDIENTE_VALIDACION");
        builder.Property(o => o.DireccionEntregaCalle).HasMaxLength(150).IsRequired();
        builder.Property(o => o.DireccionEntregaColonia).HasMaxLength(100).IsRequired();
        builder.Property(o => o.DireccionEntregaMunicipio).HasMaxLength(100).IsRequired();
        builder.Property(o => o.DireccionEntregaEstado).HasMaxLength(100).IsRequired();
        builder.Property(o => o.DireccionEntregaCp).HasMaxLength(10);
        builder.Property(o => o.DireccionEntregaReferencias).HasMaxLength(255);
        builder.Property(o => o.Total).HasColumnType("numeric(10,2)").HasDefaultValue(0);
        builder.Property(o => o.SaldoPendiente).HasColumnType("numeric(10,2)").HasDefaultValue(0);
        builder.Property(o => o.FechaCreacion).HasDefaultValueSql("NOW()");

        builder.HasMany(o => o.OrderItems)
               .WithOne(oi => oi.Order)
               .HasForeignKey(oi => oi.OrderId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(o => o.Payments)
               .WithOne(p => p.Order)
               .HasForeignKey(p => p.OrderId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(o => o.Delivery)
               .WithOne(d => d.Order)
               .HasForeignKey<Delivery>(d => d.OrderId)
               .OnDelete(DeleteBehavior.Cascade);
    }
}
