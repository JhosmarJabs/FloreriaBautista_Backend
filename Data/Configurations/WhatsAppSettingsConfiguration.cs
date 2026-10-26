using FloreriaBautista.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FloreriaBautista.Data.Configurations;

public class WhatsAppSettingsConfiguration : IEntityTypeConfiguration<WhatsAppSettings>
{
    public void Configure(EntityTypeBuilder<WhatsAppSettings> builder)
    {
        builder.ToTable("whatsapp_settings", t =>
        {
            t.HasCheckConstraint("CK_whatsapp_settings_singleton", "id = 1");
            t.HasCheckConstraint("CK_whatsapp_settings_estado_conexion",
                "estado_conexion IN ('NO_CONFIGURADO','CONECTADO','CONECTANDO','DESCONECTADO','SERVICIO_NO_DISPONIBLE')");
            t.HasCheckConstraint("CK_whatsapp_settings_accion_pendiente",
                "accion_pendiente IN ('NINGUNA','APLICAR_VERSION','REINICIAR')");
            t.HasCheckConstraint("CK_whatsapp_settings_estado_accion",
                "estado_accion IN ('NINGUNA','PENDIENTE','APLICANDO','DESPLEGADO','APLICADA','ERROR')");
        });
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).HasDefaultValue(1);
        builder.Property(s => s.Habilitado).HasDefaultValue(false);

        builder.Property(s => s.EstadoConexion).HasMaxLength(30).HasDefaultValue("DESCONECTADO");
        builder.Property(s => s.NumeroVinculado).HasMaxLength(20);
        builder.Property(s => s.AvisoDesconexionEnviado).HasDefaultValue(false);

        builder.Property(s => s.NotificarPedidoNuevo).HasDefaultValue(true);
        builder.Property(s => s.NotificarCambioEstado).HasDefaultValue(true);
        builder.Property(s => s.EstadosNotificados).HasMaxLength(200)
               .HasDefaultValue("EN_RUTA,ENTREGADO,CANCELADO,PENDIENTE_ANULACION");
        builder.Property(s => s.NotificarStockBajo).HasDefaultValue(true);
        builder.Property(s => s.NotificarVentaInstantanea).HasDefaultValue(true);
        builder.Property(s => s.NotificarReabastecimiento).HasDefaultValue(true);

        builder.Property(s => s.VersionDeseada).HasMaxLength(30);
        builder.Property(s => s.VersionAplicada).HasMaxLength(30);
        builder.Property(s => s.AccionPendiente).HasMaxLength(20).HasDefaultValue("NINGUNA");
        builder.Property(s => s.EstadoAccion).HasMaxLength(20).HasDefaultValue("NINGUNA");
        builder.Property(s => s.AccionMensaje).HasMaxLength(500);
        builder.Property(s => s.ActualizadoEn).HasDefaultValueSql("NOW()");

        // Fecha fija para que la semilla no cambie en cada migración nueva.
        builder.HasData(new WhatsAppSettings
        {
            Id            = 1,
            ActualizadoEn = new DateTime(2026, 10, 14, 0, 0, 0, DateTimeKind.Utc)
        });
    }
}
