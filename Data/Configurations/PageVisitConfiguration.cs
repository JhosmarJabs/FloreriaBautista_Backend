using FloreriaBautista.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FloreriaBautista.Data.Configurations;

public class PageVisitConfiguration : IEntityTypeConfiguration<PageVisit>
{
    public void Configure(EntityTypeBuilder<PageVisit> builder)
    {
        builder.ToTable("page_visits");
        builder.HasKey(v => v.Id);
        builder.Property(v => v.Id).UseIdentityByDefaultColumn();

        builder.Property(v => v.Ruta).HasMaxLength(200).IsRequired();
        builder.Property(v => v.SesionId).HasMaxLength(64).IsRequired();
        builder.Property(v => v.Referrer).HasMaxLength(120);
        builder.Property(v => v.Dispositivo).HasMaxLength(20).HasDefaultValue("DESKTOP");
        builder.Property(v => v.Busqueda).HasMaxLength(120);
        builder.Property(v => v.FechaHora).HasDefaultValueSql("NOW()");

        // El reporte siempre filtra por rango de fechas; el resto de los cortes
        // (ruta, producto, sesión) se hace dentro de ese rango.
        builder.HasIndex(v => v.FechaHora);
        builder.HasIndex(v => new { v.FechaHora, v.Ruta });
        builder.HasIndex(v => v.SesionId);
        builder.HasIndex(v => v.ProductId).HasFilter("product_id IS NOT NULL");

        // Las visitas son datos de análisis: borrar un producto o un usuario no
        // debe borrar el histórico, solo desligarlo.
        builder.HasOne(v => v.Usuario)
               .WithMany()
               .HasForeignKey(v => v.UsuarioId)
               .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(v => v.Product)
               .WithMany()
               .HasForeignKey(v => v.ProductId)
               .OnDelete(DeleteBehavior.SetNull);
    }
}
