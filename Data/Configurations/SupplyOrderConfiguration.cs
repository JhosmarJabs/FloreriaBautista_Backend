using FloreriaBautista.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FloreriaBautista.Data.Configurations;

public class SupplyOrderConfiguration : IEntityTypeConfiguration<SupplyOrder>
{
    public void Configure(EntityTypeBuilder<SupplyOrder> builder)
    {
        builder.ToTable("solicitud_reabastecimiento");
        builder.HasKey(o => o.Id);
        builder.Property(o => o.Id).HasDefaultValueSql("gen_random_uuid()");

        builder.Property(o => o.Folio).HasMaxLength(20).IsRequired();
        builder.HasIndex(o => o.Folio).IsUnique();

        builder.Property(o => o.Estado).HasMaxLength(20).HasDefaultValue("BORRADOR").IsRequired();
        builder.Property(o => o.Proveedor).HasMaxLength(150);
        builder.Property(o => o.SemanaObjetivo).HasMaxLength(20);
        builder.Property(o => o.Notas).HasMaxLength(500);
        builder.Property(o => o.FechaSolicitud).HasDefaultValueSql("NOW()");

        // Sin .HasDefaultValue(0): igual que en orders, un DEFAULT declarado aquí haría que EF
        // omitiera la columna del INSERT cuando el total es 0 y Postgres insertaría NULL.
        builder.Property(o => o.TotalEstimado).HasColumnType("numeric(10,2)");

        builder.HasIndex(o => o.Estado);
        builder.HasIndex(o => o.FechaSolicitud);

        builder.HasOne(o => o.Usuario)
               .WithMany()
               .HasForeignKey(o => o.UsuarioId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(o => o.Items)
               .WithOne(i => i.SupplyOrder)
               .HasForeignKey(i => i.SupplyOrderId)
               .OnDelete(DeleteBehavior.Cascade);
    }
}
