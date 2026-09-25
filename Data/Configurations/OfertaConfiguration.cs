using FloreriaBautista.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FloreriaBautista.Data.Configurations;

public class OfertaConfiguration : IEntityTypeConfiguration<Oferta>
{
    public void Configure(EntityTypeBuilder<Oferta> builder)
    {
        builder.ToTable("ofertas");
        builder.HasKey(o => o.Id);
        builder.Property(o => o.Id).HasDefaultValueSql("gen_random_uuid()");
        builder.Property(o => o.PrecioOferta).HasColumnType("numeric(10,2)").IsRequired();
        builder.Property(o => o.Activo).HasDefaultValue(true);
        builder.Property(o => o.CreadoEn).HasDefaultValueSql("NOW()");
        builder.Property(o => o.ActualizadoEn).HasDefaultValueSql("NOW()");

        builder.HasOne(o => o.Producto)
               .WithMany()
               .HasForeignKey(o => o.ProductoId)
               .OnDelete(DeleteBehavior.Cascade);

        // Solo una oferta activa por producto a la vez.
        builder.HasIndex(o => o.ProductoId)
               .IsUnique()
               .HasFilter("activo = true");
    }
}
