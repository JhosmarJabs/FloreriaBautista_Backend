using FloreriaBautista.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FloreriaBautista.Data.Configurations;

public class SiteSettingsConfiguration : IEntityTypeConfiguration<SiteSettings>
{
    public void Configure(EntityTypeBuilder<SiteSettings> builder)
    {
        builder.ToTable("site_settings");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.NombreTienda).HasMaxLength(150);
        builder.Property(s => s.Telefono).HasMaxLength(30);
        builder.Property(s => s.Correo).HasMaxLength(150);
        builder.Property(s => s.Direccion).HasMaxLength(255);
        builder.Property(s => s.BannerTitulo).HasMaxLength(200);
        builder.Property(s => s.BannerSubtitulo).HasMaxLength(255);
        builder.Property(s => s.BannerCta).HasMaxLength(60);
        builder.Property(s => s.HorariosJson).HasColumnType("text").HasDefaultValue("[]");
        builder.Property(s => s.DestacadosJson).HasColumnType("text").HasDefaultValue("[]");
        builder.Property(s => s.AnuncioTexto).HasMaxLength(255);
        builder.Property(s => s.ActualizadoEn).HasDefaultValueSql("NOW()");
    }
}
