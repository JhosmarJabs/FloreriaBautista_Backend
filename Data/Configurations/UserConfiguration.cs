using FloreriaBautista.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FloreriaBautista.Data.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users");
        builder.HasKey(u => u.Id);
        builder.Property(u => u.Id).HasDefaultValueSql("gen_random_uuid()");
        builder.Property(u => u.Nombre).HasMaxLength(100).IsRequired();
        builder.Property(u => u.Apellido).HasColumnName("apellido").HasMaxLength(100).IsRequired();
        builder.Property(u => u.Correo).HasMaxLength(150);
        builder.HasIndex(u => u.Correo).IsUnique();
        builder.Property(u => u.Telefono).HasMaxLength(20);
        builder.Property(u => u.Sexo).HasMaxLength(10);
        builder.Property(u => u.PasswordHash).HasMaxLength(255);
        builder.Property(u => u.Estado).HasMaxLength(20).HasDefaultValue("ACTIVO");
        builder.Property(u => u.CreadoEn).HasDefaultValueSql("NOW()");
        builder.Property(u => u.ActualizadoEn).HasDefaultValueSql("NOW()");

        builder.HasMany(u => u.UserRoles)
               .WithOne(ur => ur.User)
               .HasForeignKey(ur => ur.UserId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(u => u.AuthTokens)
               .WithOne(t => t.User)
               .HasForeignKey(t => t.UserId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(u => u.Customer)
               .WithOne(c => c.User)
               .HasForeignKey<Customer>(c => c.UserId)
               .OnDelete(DeleteBehavior.SetNull);
    }
}
