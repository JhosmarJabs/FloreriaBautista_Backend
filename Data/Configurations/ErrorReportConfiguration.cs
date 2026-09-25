using FloreriaBautista.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FloreriaBautista.Data.Configurations;

public class ErrorReportConfiguration : IEntityTypeConfiguration<ErrorReport>
{
    public void Configure(EntityTypeBuilder<ErrorReport> builder)
    {
        builder.ToTable("error_reports");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).HasDefaultValueSql("gen_random_uuid()");
        builder.Property(r => r.TipoRegistro).HasMaxLength(20).IsRequired();
        builder.Property(r => r.Motivo).HasMaxLength(1000).IsRequired();
        builder.Property(r => r.Estado).HasMaxLength(20).HasDefaultValue("PENDIENTE");
        builder.Property(r => r.FechaHora).HasDefaultValueSql("NOW()");
        builder.Property(r => r.ResolucionAccion).HasMaxLength(20);
        builder.Property(r => r.ResolucionNota).HasMaxLength(1000);

        builder.HasOne(r => r.Usuario)
               .WithMany()
               .HasForeignKey(r => r.UsuarioId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(r => r.ResueltoPor)
               .WithMany()
               .HasForeignKey(r => r.ResueltoPorUsuarioId)
               .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(r => new { r.UsuarioId, r.FechaHora });
        builder.HasIndex(r => new { r.TipoRegistro, r.RegistroId });

        // La bandeja del admin entra por aquí: los pendientes primero.
        builder.HasIndex(r => r.Estado);
    }
}
