using FloreriaBautista.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FloreriaBautista.Data.Configurations;

public class WhatsAppRecipientConfiguration : IEntityTypeConfiguration<WhatsAppRecipient>
{
    public void Configure(EntityTypeBuilder<WhatsAppRecipient> builder)
    {
        builder.ToTable("whatsapp_recipients", t =>
            t.HasCheckConstraint("CK_whatsapp_recipients_telefono", "telefono ~ '^52[0-9]{10,11}$'"));
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).HasDefaultValueSql("gen_random_uuid()");
        builder.Property(r => r.Nombre).HasMaxLength(100).IsRequired();
        builder.Property(r => r.Telefono).HasMaxLength(15).IsRequired();
        builder.Property(r => r.Activo).HasDefaultValue(true);
        builder.Property(r => r.CreadoEn).HasDefaultValueSql("NOW()");
        builder.Property(r => r.ActualizadoEn).HasDefaultValueSql("NOW()");

        builder.HasIndex(r => r.Telefono).IsUnique();
    }
}
