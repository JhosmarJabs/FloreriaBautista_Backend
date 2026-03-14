using FloreriaBautista.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FloreriaBautista.Data.Configurations;

public class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("audit_logs");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).HasDefaultValueSql("gen_random_uuid()");
        builder.Property(a => a.Accion).HasMaxLength(50).IsRequired();
        builder.Property(a => a.Entidad).HasMaxLength(50).IsRequired();
        builder.Property(a => a.EntidadId).HasMaxLength(100);
        builder.Property(a => a.FechaHora).HasDefaultValueSql("NOW()");

        builder.HasIndex(a => a.UsuarioId);
        builder.HasIndex(a => a.FechaHora);
        builder.HasIndex(a => new { a.Entidad, a.EntidadId });
    }
}
