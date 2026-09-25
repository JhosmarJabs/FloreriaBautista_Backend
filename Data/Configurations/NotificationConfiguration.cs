using FloreriaBautista.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FloreriaBautista.Data.Configurations;

public class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.ToTable("notifications");
        builder.HasKey(n => n.Id);
        builder.Property(n => n.Id).HasDefaultValueSql("gen_random_uuid()");
        builder.Property(n => n.Tipo).HasMaxLength(50).IsRequired();
        builder.Property(n => n.Titulo).HasMaxLength(200).IsRequired();
        builder.Property(n => n.Mensaje).IsRequired();
        builder.Property(n => n.EntidadTipo).HasMaxLength(50);
        builder.Property(n => n.Leida).HasDefaultValue(false);
        builder.Property(n => n.CreadaEn).HasDefaultValueSql("NOW()");

        builder.HasOne(n => n.Usuario)
            .WithMany()
            .HasForeignKey(n => n.DestinatarioUsuarioId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(n => n.Customer)
            .WithMany()
            .HasForeignKey(n => n.DestinatarioCustomerId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(n => new { n.DestinatarioUsuarioId, n.Leida, n.CreadaEn })
            .HasDatabaseName("ix_notifications_destinatario_usuario")
            .IsDescending(false, false, true);

        builder.HasIndex(n => new { n.DestinatarioCustomerId, n.Leida, n.CreadaEn })
            .HasDatabaseName("ix_notifications_destinatario_customer")
            .IsDescending(false, false, true);

        builder.ToTable(t => t.HasCheckConstraint(
            "chk_un_destinatario",
            "(destinatario_usuario_id IS NOT NULL) <> (destinatario_customer_id IS NOT NULL)"));
    }
}
