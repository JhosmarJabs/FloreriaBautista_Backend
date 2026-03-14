using FloreriaBautista.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FloreriaBautista.Data.Configurations;

public class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        builder.ToTable("payments");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).HasDefaultValueSql("gen_random_uuid()");
        builder.Property(p => p.Monto).HasColumnType("numeric(10,2)").IsRequired();
        builder.Property(p => p.TipoPago).HasMaxLength(20).IsRequired();
        builder.Property(p => p.Metodo).HasMaxLength(20).IsRequired();
        builder.Property(p => p.Estado).HasMaxLength(20).HasDefaultValue("REGISTRADO");
        builder.Property(p => p.FechaPago).HasDefaultValueSql("NOW()");
    }
}
