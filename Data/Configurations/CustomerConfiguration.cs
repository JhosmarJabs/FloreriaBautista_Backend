using FloreriaBautista.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FloreriaBautista.Data.Configurations;

public class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> builder)
    {
        builder.ToTable("customers");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).HasDefaultValueSql("gen_random_uuid()");
        builder.Property(c => c.TipoCliente).HasMaxLength(20).IsRequired();
        builder.Property(c => c.Nombre).HasMaxLength(100).IsRequired();
        builder.Property(c => c.Apellido).HasMaxLength(100);
        builder.Property(c => c.Telefono).HasMaxLength(20).IsRequired();
        builder.Property(c => c.Correo).HasMaxLength(150);
        builder.Property(c => c.Sexo).HasMaxLength(10);
        builder.Property(c => c.Rfc).HasMaxLength(15);
        builder.Property(c => c.RazonSocial).HasMaxLength(150);
        builder.Property(c => c.CpFiscal).HasMaxLength(10);
        builder.Property(c => c.RegimenFiscal).HasMaxLength(50);
        builder.Property(c => c.CreadoEn).HasDefaultValueSql("NOW()");

        builder.HasIndex(c => c.Telefono);
        builder.HasIndex(c => c.UserId).HasFilter("user_id IS NOT NULL");

        builder.HasMany(c => c.Addresses)
               .WithOne(a => a.Customer)
               .HasForeignKey(a => a.CustomerId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(c => c.Orders)
               .WithOne(o => o.Customer)
               .HasForeignKey(o => o.CustomerId);
    }
}
