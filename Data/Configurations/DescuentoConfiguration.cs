using FloreriaBautista.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FloreriaBautista.Data.Configurations;

public class DescuentoConfiguration : IEntityTypeConfiguration<Descuento>
{
    public void Configure(EntityTypeBuilder<Descuento> builder)
    {
        builder.ToTable("descuentos");
        builder.HasKey(d => d.Id);
        builder.Property(d => d.Id).HasDefaultValueSql("gen_random_uuid()");
        builder.Property(d => d.Nombre).HasMaxLength(150).IsRequired();
        builder.Property(d => d.TipoRegla).HasMaxLength(30).IsRequired();
        builder.Property(d => d.MontoMinimoCompra).HasColumnType("numeric(10,2)");
        builder.Property(d => d.TipoValor).HasMaxLength(20).IsRequired();
        builder.Property(d => d.Valor).HasColumnType("numeric(10,2)").IsRequired();
        builder.Property(d => d.Activo).HasDefaultValue(true);
        builder.Property(d => d.CreadoEn).HasDefaultValueSql("NOW()");
        builder.Property(d => d.ActualizadoEn).HasDefaultValueSql("NOW()");

        builder.HasOne(d => d.Categoria)
               .WithMany()
               .HasForeignKey(d => d.CategoriaId)
               .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(d => d.Catalogo)
               .WithMany()
               .HasForeignKey(d => d.CatalogoId)
               .OnDelete(DeleteBehavior.SetNull);
    }
}
