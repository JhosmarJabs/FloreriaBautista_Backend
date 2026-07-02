using FloreriaBautista.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FloreriaBautista.Data.Configurations;

public class PromotionConfiguration : IEntityTypeConfiguration<Promotion>
{
    public void Configure(EntityTypeBuilder<Promotion> builder)
    {
        builder.ToTable("promotions");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).HasDefaultValueSql("gen_random_uuid()");
        builder.Property(p => p.Nombre).HasMaxLength(150).IsRequired();
        builder.Property(p => p.Codigo).HasMaxLength(30);
        builder.HasIndex(p => p.Codigo).IsUnique().HasFilter("codigo IS NOT NULL");
        builder.Property(p => p.Tipo).HasMaxLength(20).HasDefaultValue("PORCENTAJE");
        builder.Property(p => p.Valor).HasColumnType("numeric(10,2)").HasDefaultValue(0);
        builder.Property(p => p.MinimoCompra).HasColumnType("numeric(10,2)").HasDefaultValue(0);
        builder.Property(p => p.Estado).HasMaxLength(20).HasDefaultValue("ACTIVO");
        builder.Property(p => p.UsosActuales).HasDefaultValue(0);
        builder.Property(p => p.AplicarATodaLaTienda).HasDefaultValue(true);
        builder.Property(p => p.CreadoEn).HasDefaultValueSql("NOW()");
        builder.Property(p => p.ActualizadoEn).HasDefaultValueSql("NOW()");
    }
}
