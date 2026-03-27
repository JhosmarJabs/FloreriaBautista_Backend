using FloreriaBautista.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FloreriaBautista.Data.Configurations;

public class SchedulerSettingsConfiguration : IEntityTypeConfiguration<SchedulerSettings>
{
    public void Configure(EntityTypeBuilder<SchedulerSettings> builder)
    {
        builder.ToTable("scheduler_settings");
        builder.HasKey(s => s.Id);

        builder.Property(s => s.Frecuencia).HasMaxLength(20).HasDefaultValue("SEMANAL");
        builder.Property(s => s.FrecuenciaMantenimiento).HasMaxLength(20).HasDefaultValue("SEMANAL");
        builder.Property(s => s.HoraMantenimiento).HasDefaultValue(1);
        builder.Property(s => s.ActualizadoEn).HasDefaultValueSql("NOW()");
    }
}
