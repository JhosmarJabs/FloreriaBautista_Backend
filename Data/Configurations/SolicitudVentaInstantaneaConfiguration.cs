using FloreriaBautista.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FloreriaBautista.Data.Configurations;

public class SolicitudVentaInstantaneaConfiguration : IEntityTypeConfiguration<SolicitudVentaInstantanea>
{
    public void Configure(EntityTypeBuilder<SolicitudVentaInstantanea> builder)
    {
        builder.ToTable("solicitudes_venta_instantanea");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).HasDefaultValueSql("gen_random_uuid()");

        builder.Property(s => s.Estado).HasMaxLength(20).IsRequired().HasDefaultValue("PENDIENTE");
        builder.Property(s => s.CreadaEn).HasDefaultValueSql("NOW()");
        builder.Property(s => s.MotivoRechazo).HasMaxLength(500);
        builder.Property(s => s.MotivoExpiracion).HasMaxLength(50);

        // Indice compuesto para que el job de expiracion no haga table scan cada 15-30s.
        builder.HasIndex(s => new { s.Estado, s.CreadaEn });

        // FK a Customer (Restrict: no borrar cliente con solicitudes).
        builder.HasOne(s => s.Customer)
               .WithMany()
               .HasForeignKey(s => s.CustomerId)
               .OnDelete(DeleteBehavior.Restrict);

        // FK a Product (Restrict, mismo patron que ProductRecipe).
        builder.HasOne(s => s.Product)
               .WithMany()
               .HasForeignKey(s => s.ProductId)
               .OnDelete(DeleteBehavior.Restrict);

        // FK a User (quien decidio). Nullable; SetNull al dar de baja al usuario.
        builder.HasOne(s => s.DecididaPor)
               .WithMany()
               .HasForeignKey(s => s.DecididaPorUsuarioId)
               .OnDelete(DeleteBehavior.SetNull);

        // FK a Order (nullable, se llena cuando el pedido nace).
        builder.HasOne(s => s.Order)
               .WithMany()
               .HasForeignKey(s => s.OrderId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(s => s.OrderId)
               .IsUnique()
               .HasFilter("order_id IS NOT NULL");
    }
}
