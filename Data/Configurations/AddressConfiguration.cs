using FloreriaBautista.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FloreriaBautista.Data.Configurations;

public class AddressConfiguration : IEntityTypeConfiguration<Address>
{
    public void Configure(EntityTypeBuilder<Address> builder)
    {
        builder.ToTable("addresses");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Calle).HasMaxLength(200).IsRequired();
        builder.Property(a => a.Colonia).HasMaxLength(100).IsRequired();
        builder.Property(a => a.Municipio).HasMaxLength(100).IsRequired();
        builder.Property(a => a.Estado).HasMaxLength(100).IsRequired();
        builder.Property(a => a.Cp).HasMaxLength(10);
        builder.Property(a => a.Etiqueta).HasMaxLength(50);
        builder.Property(a => a.Referencias).HasMaxLength(500);
        builder.Property(a => a.EsPrincipal).HasDefaultValue(false);
        builder.Property(a => a.CreadoEn).HasDefaultValueSql("NOW()");
    }
}
