using FloreriaBautista.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FloreriaBautista.Data.Configurations;

public class BackupJobConfiguration : IEntityTypeConfiguration<BackupJob>
{
    public void Configure(EntityTypeBuilder<BackupJob> builder)
    {
        builder.ToTable("backup_jobs");
        builder.HasKey(b => b.Id);
        builder.Property(b => b.Id).HasDefaultValueSql("gen_random_uuid()");
        builder.Property(b => b.Tipo).HasMaxLength(20).IsRequired();
        builder.Property(b => b.Estado).HasMaxLength(20).HasDefaultValue("PENDIENTE");
        builder.Property(b => b.NombreTabla).HasMaxLength(100);
        builder.Property(b => b.Descripcion).HasMaxLength(500);
        builder.Property(b => b.Formato).HasMaxLength(10).HasDefaultValue("BACKUP");
        builder.Property(b => b.DriveFileId).HasMaxLength(255);
        builder.Property(b => b.CreadoEn).HasDefaultValueSql("NOW()");
    }
}
