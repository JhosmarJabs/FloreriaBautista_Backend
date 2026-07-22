using FloreriaBautista.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FloreriaBautista.Data.Configurations;

public class AssociationRuleConfiguration : IEntityTypeConfiguration<AssociationRule>
{
    public void Configure(EntityTypeBuilder<AssociationRule> builder)
    {
        builder.ToTable("association_rules");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).HasDefaultValueSql("gen_random_uuid()");
        builder.Property(r => r.Soporte).HasColumnType("numeric(6,4)");
        builder.Property(r => r.Confianza).HasColumnType("numeric(6,4)");
        builder.Property(r => r.CalculadoEn).HasDefaultValueSql("NOW()");

        builder.HasIndex(r => new { r.ProductIdAntecedente, r.ProductIdConsecuente }).IsUnique();
        builder.HasIndex(r => r.ProductIdAntecedente);

        builder.HasOne(r => r.ProductoAntecedente)
               .WithMany()
               .HasForeignKey(r => r.ProductIdAntecedente)
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(r => r.ProductoConsecuente)
               .WithMany()
               .HasForeignKey(r => r.ProductIdConsecuente)
               .OnDelete(DeleteBehavior.Restrict);
    }
}
