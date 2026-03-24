using FloreriaBautista.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FloreriaBautista.Data.Configurations;

public class FlowerConfiguration : IEntityTypeConfiguration<Flower>
{
    public void Configure(EntityTypeBuilder<Flower> builder)
    {
        builder.ToTable("flowers");
        builder.HasKey(f => f.Id);
        builder.Property(f => f.Nombre).HasMaxLength(100).IsRequired();
        builder.Property(f => f.Color).HasMaxLength(50).IsRequired();
        builder.Property(f => f.PrecioCosto).HasColumnType("decimal(10,2)");
        builder.Property(f => f.UnidadMedida).HasMaxLength(30).HasDefaultValue("TALLO");
        builder.Property(f => f.EsFlorPrimaria).HasDefaultValue(false);
        builder.Property(f => f.Estado).HasMaxLength(20).HasDefaultValue("ACTIVA");
        builder.Property(f => f.CreadoEn).HasDefaultValueSql("NOW()");
        builder.Property(f => f.ActualizadoEn).HasDefaultValueSql("NOW()");

        builder.HasMany(f => f.FlowerMovements)
               .WithOne(m => m.Flower)
               .HasForeignKey(m => m.FlowerId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(f => f.ProductFlowers)
               .WithOne(pf => pf.Flower)
               .HasForeignKey(pf => pf.FlowerId)
               .OnDelete(DeleteBehavior.Cascade);
    }
}
