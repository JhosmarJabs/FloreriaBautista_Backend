using FloreriaBautista.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FloreriaBautista.Data.Configurations;

public class ImportJobConfiguration : IEntityTypeConfiguration<ImportJob>
{
    public void Configure(EntityTypeBuilder<ImportJob> builder)
    {
        builder.ToTable("import_jobs");
        builder.HasKey(i => i.Id);
        builder.Property(i => i.Id).HasDefaultValueSql("gen_random_uuid()");
        builder.Property(i => i.TipoImportacion).HasMaxLength(30).IsRequired();
        builder.Property(i => i.Estado).HasMaxLength(20).HasDefaultValue("PENDIENTE");
        builder.Property(i => i.CreadoEn).HasDefaultValueSql("NOW()");
    }
}
