using FloreriaBautista.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FloreriaBautista.Data.Configurations;

public class QuickSaleTemplateItemConfiguration : IEntityTypeConfiguration<QuickSaleTemplateItem>
{
    public void Configure(EntityTypeBuilder<QuickSaleTemplateItem> builder)
    {
        builder.ToTable("quick_sale_template_items");
        builder.HasKey(i => i.Id);
        builder.Property(i => i.Id).HasDefaultValueSql("gen_random_uuid()");
        builder.Property(i => i.Icono).HasMaxLength(30).HasDefaultValue("Sparkles");
        builder.Property(i => i.Color).HasMaxLength(30).HasDefaultValue("blue");
        builder.Property(i => i.Orden).HasDefaultValue(0);
        builder.Property(i => i.CantidadPreset).HasDefaultValue(1);

        builder.HasOne(i => i.Template)
               .WithMany(t => t.Items)
               .HasForeignKey(i => i.QuickSaleTemplateId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(i => i.Product)
               .WithMany(p => p.QuickSaleTemplateItems)
               .HasForeignKey(i => i.ProductId)
               .OnDelete(DeleteBehavior.Cascade);
    }
}
