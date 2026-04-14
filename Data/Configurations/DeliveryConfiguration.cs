using FloreriaBautista.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FloreriaBautista.Data.Configurations;

public class DeliveryConfiguration : IEntityTypeConfiguration<Delivery>
{
    public void Configure(EntityTypeBuilder<Delivery> builder)
    {
        builder.ToTable("deliveries");
        builder.HasKey(d => d.Id);
        builder.Property(d => d.Id).HasDefaultValueSql("gen_random_uuid()");
        builder.Property(d => d.EstadoEntrega).HasMaxLength(30).IsRequired();
        builder.Property(d => d.EvidenciaFotografica).HasMaxLength(500);
        builder.Property(d => d.FirmaReceptor).HasMaxLength(500);
        builder.Property(d => d.Notas);
    }
}
