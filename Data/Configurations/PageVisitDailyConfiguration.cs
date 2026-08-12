using FloreriaBautista.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FloreriaBautista.Data.Configurations;

public class PageVisitDailyConfiguration : IEntityTypeConfiguration<PageVisitDaily>
{
    public void Configure(EntityTypeBuilder<PageVisitDaily> builder)
    {
        builder.ToTable("page_visits_daily");
        builder.HasKey(v => v.Id);
        builder.Property(v => v.Id).HasDefaultValueSql("gen_random_uuid()");

        builder.Property(v => v.Ruta).HasMaxLength(200).IsRequired();
        builder.Property(v => v.Dispositivo).HasMaxLength(20).IsRequired();
        builder.Property(v => v.CalculadoEn).HasDefaultValueSql("NOW()");

        // El rollup es idempotente: recalcula un día y sobrescribe su fila.
        builder.HasIndex(v => new { v.Fecha, v.Ruta, v.Dispositivo }).IsUnique();
    }
}
