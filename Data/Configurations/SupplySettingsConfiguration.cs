using FloreriaBautista.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FloreriaBautista.Data.Configurations;

public class SupplySettingsConfiguration : IEntityTypeConfiguration<SupplySettings>
{
    public void Configure(EntityTypeBuilder<SupplySettings> builder)
    {
        builder.ToTable("supply_settings");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).HasDefaultValue(1);
        builder.HasCheckConstraint("CK_supply_settings_singleton", "id = 1");
        builder.Property(s => s.DiaAbastecimiento).HasDefaultValue(1);
        builder.Property(s => s.HoraCutoff).HasDefaultValueSql("'12:00'::time");
        builder.Property(s => s.DiaCutoff).HasDefaultValue(6);
        builder.Property(s => s.ToleranciaUnidades).HasDefaultValue(5);
        builder.Property(s => s.ToleranciaPorcentaje)
               .HasColumnType("numeric(5,4)")
               .HasDefaultValue(0.10m);
        builder.Property(s => s.UsarToleranciaAbsoluta).HasDefaultValue(true);
        builder.Property(s => s.MargenSeguridadPorcentaje)
               .HasColumnType("numeric(5,4)")
               .HasDefaultValue(0.10m);
        builder.Property(s => s.DiasMinimosAnticipacion).HasDefaultValue(7);
        builder.Property(s => s.PermitirVentaSinReceta).HasDefaultValue(true);
        builder.Property(s => s.ActualizadoEn).HasDefaultValueSql("NOW()");

        builder.HasData(new SupplySettings { Id = 1 });
    }
}
