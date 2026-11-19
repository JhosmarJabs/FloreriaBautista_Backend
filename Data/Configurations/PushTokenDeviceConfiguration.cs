using FloreriaBautista.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FloreriaBautista.Data.Configurations;

public class PushTokenDeviceConfiguration : IEntityTypeConfiguration<PushTokenDevice>
{
    public void Configure(EntityTypeBuilder<PushTokenDevice> builder)
    {
        builder.ToTable("push_token_devices");

        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).HasColumnName("id");
        builder.Property(p => p.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(p => p.Token).HasColumnName("token").IsRequired().HasMaxLength(512);
        builder.Property(p => p.Platform).HasColumnName("platform").HasMaxLength(20).HasDefaultValue("android");
        builder.Property(p => p.ActualizadoEn).HasColumnName("actualizado_en");

        builder.HasOne(p => p.User)
            .WithMany()
            .HasForeignKey(p => p.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // Un token por dispositivo: el teléfono avisa solo a la última persona que inició sesión en él.
        builder.HasIndex(p => p.Token).IsUnique();
    }
}
