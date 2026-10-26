using FloreriaBautista.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FloreriaBautista.Data.Configurations;

public class WhatsAppMessageConfiguration : IEntityTypeConfiguration<WhatsAppMessage>
{
    public void Configure(EntityTypeBuilder<WhatsAppMessage> builder)
    {
        builder.ToTable("whatsapp_messages", t =>
            t.HasCheckConstraint("CK_whatsapp_messages_estado",
                "estado IN ('PENDIENTE','ENVIADO','FALLIDO','EXPIRADO')"));
        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).HasDefaultValueSql("gen_random_uuid()");
        builder.Property(m => m.TipoEvento).HasMaxLength(40).IsRequired();
        builder.Property(m => m.EntidadTipo).HasMaxLength(50);
        builder.Property(m => m.Texto).HasMaxLength(1000).IsRequired();
        builder.Property(m => m.Estado).HasMaxLength(15).HasDefaultValue("PENDIENTE");
        builder.Property(m => m.Intentos).HasDefaultValue(0);
        builder.Property(m => m.UltimoError).HasMaxLength(500);
        builder.Property(m => m.CreadoEn).HasDefaultValueSql("NOW()");

        builder.HasIndex(m => new { m.Estado, m.CreadoEn });
        builder.HasIndex(m => new { m.TipoEvento, m.EntidadId, m.CreadoEn });

        builder.HasOne(m => m.Recipient)
               .WithMany()
               .HasForeignKey(m => m.RecipientId)
               .OnDelete(DeleteBehavior.Restrict);
    }
}
