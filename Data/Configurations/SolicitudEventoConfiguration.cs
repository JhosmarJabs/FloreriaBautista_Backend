using FloreriaBautista.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FloreriaBautista.Data.Configurations;

public class SolicitudEventoConfiguration : IEntityTypeConfiguration<SolicitudEvento>
{
    public void Configure(EntityTypeBuilder<SolicitudEvento> builder)
    {
        builder.ToTable("solicitudes_evento");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).HasDefaultValueSql("gen_random_uuid()");
        builder.Property(s => s.TipoEvento).HasMaxLength(30).IsRequired();
        builder.Property(s => s.NombreContacto).HasMaxLength(150).IsRequired();
        builder.Property(s => s.TelefonoContacto).HasMaxLength(20).IsRequired();
        builder.Property(s => s.CorreoContacto).HasMaxLength(150);
        builder.Property(s => s.Mensaje).HasMaxLength(1000);
        builder.Property(s => s.Estado).HasMaxLength(20).IsRequired()
            .HasDefaultValue(EstadosSolicitudEvento.Pendiente);
        builder.Property(s => s.CreadoEn).HasDefaultValueSql("NOW()");
        builder.Property(s => s.ActualizadoEn).HasDefaultValueSql("NOW()");

        builder.HasOne(s => s.Customer)
            .WithMany()
            .HasForeignKey(s => s.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(s => new { s.Estado, s.CreadoEn })
            .HasDatabaseName("ix_solicitudes_evento_estado_creado")
            .IsDescending(false, true);

        builder.HasIndex(s => new { s.CustomerId, s.Estado })
            .HasDatabaseName("ix_solicitudes_evento_customer_estado");

        builder.ToTable(t =>
        {
            t.HasCheckConstraint("chk_solicitud_evento_tipo",
                "tipo_evento IN ('BODA','BAUTIZO','CORPORATIVO','CONMEMORATIVO','OTRO')");
            t.HasCheckConstraint("chk_solicitud_evento_estado",
                "estado IN ('PENDIENTE','CONTACTADA','CERRADA')");
            t.HasCheckConstraint("chk_solicitud_evento_invitados",
                "invitados_aprox IS NULL OR invitados_aprox BETWEEN 1 AND 5000");
        });
    }
}
