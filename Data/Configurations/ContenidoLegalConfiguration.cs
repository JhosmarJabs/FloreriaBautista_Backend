using FloreriaBautista.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FloreriaBautista.Data.Configurations;

public class ContenidoLegalConfiguration : IEntityTypeConfiguration<ContenidoLegal>
{
    public void Configure(EntityTypeBuilder<ContenidoLegal> builder)
    {
        builder.ToTable("contenido_legal");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Slug).HasMaxLength(60).IsRequired();
        builder.HasIndex(c => c.Slug).IsUnique();
        builder.Property(c => c.Titulo).HasMaxLength(150).IsRequired();
        builder.Property(c => c.SeccionesJson).HasColumnType("text").HasDefaultValue("[]");
        builder.Property(c => c.NotasRevision).HasColumnType("text");
        builder.Property(c => c.EsProvisional).HasDefaultValue(true);
        builder.Property(c => c.ActualizadoEn).HasColumnType("timestamp with time zone").HasDefaultValueSql("NOW()");
    }
}
