using FloreriaBautista.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FloreriaBautista.Data.Configurations;

public class OrderItemCustomizationConfiguration : IEntityTypeConfiguration<OrderItemCustomization>
{
    public void Configure(EntityTypeBuilder<OrderItemCustomization> builder)
    {
        builder.ToTable("order_item_customizations");
        builder.HasKey(oic => oic.Id);
        builder.Property(oic => oic.Valor).HasMaxLength(255);
    }
}
