using FloreriaBautista.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FloreriaBautista.Data.Configurations;

public class CustomizationOptionConfiguration : IEntityTypeConfiguration<CustomizationOption>
{
    public void Configure(EntityTypeBuilder<CustomizationOption> builder)
    {
        builder.ToTable("customization_options");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Clave).HasMaxLength(100).IsRequired();
        builder.Property(c => c.Nombre).HasMaxLength(100).IsRequired();
        builder.Property(c => c.Tipo).HasMaxLength(50).IsRequired();
        builder.Property(c => c.Opciones).HasColumnType("text");
    }
}
