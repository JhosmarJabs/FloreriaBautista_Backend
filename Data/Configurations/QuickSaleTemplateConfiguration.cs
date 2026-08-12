using FloreriaBautista.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FloreriaBautista.Data.Configurations;

public class QuickSaleTemplateConfiguration : IEntityTypeConfiguration<QuickSaleTemplate>
{
    public void Configure(EntityTypeBuilder<QuickSaleTemplate> builder)
    {
        builder.ToTable("quick_sale_templates");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).HasDefaultValueSql("gen_random_uuid()");
        builder.Property(t => t.Nombre).HasMaxLength(100).IsRequired();
        builder.Property(t => t.Descripcion).HasMaxLength(255);
        builder.Property(t => t.Icono).HasMaxLength(30).HasDefaultValue("Sparkles");
        builder.Property(t => t.Orden).HasDefaultValue(0);
        builder.Property(t => t.Activa).HasDefaultValue(true);
        builder.Property(t => t.CreadoEn).HasDefaultValueSql("NOW()");
        builder.Property(t => t.ActualizadoEn).HasDefaultValueSql("NOW()");
    }
}
