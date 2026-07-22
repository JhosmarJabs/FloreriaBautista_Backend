using FloreriaBautista.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FloreriaBautista.Data.Configurations;

public class CustomerSegmentConfiguration : IEntityTypeConfiguration<CustomerSegment>
{
    public void Configure(EntityTypeBuilder<CustomerSegment> builder)
    {
        builder.ToTable("customer_segments");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).HasDefaultValueSql("gen_random_uuid()");
        builder.Property(s => s.Grupo).HasMaxLength(20).IsRequired();
        builder.Property(s => s.MontoTotal).HasColumnType("numeric(10,2)");
        builder.Property(s => s.FechaCalculo).HasDefaultValueSql("NOW()");

        builder.HasIndex(s => s.CustomerId).IsUnique();

        builder.HasOne(s => s.Customer)
               .WithMany()
               .HasForeignKey(s => s.CustomerId)
               .OnDelete(DeleteBehavior.Cascade);
    }
}
