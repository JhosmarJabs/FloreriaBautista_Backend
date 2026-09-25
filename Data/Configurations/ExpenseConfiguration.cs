using FloreriaBautista.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FloreriaBautista.Data.Configurations;

public class ExpenseConfiguration : IEntityTypeConfiguration<Expense>
{
    public void Configure(EntityTypeBuilder<Expense> builder)
    {
        builder.ToTable("expenses");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasDefaultValueSql("gen_random_uuid()");
        builder.Property(e => e.Concepto).HasMaxLength(200).IsRequired();
        builder.Property(e => e.Categoria).HasMaxLength(40).HasDefaultValue("GASTO_PERSONAL");
        builder.Property(e => e.Importe).HasColumnType("numeric(10,2)").IsRequired();
        builder.Property(e => e.Estado).HasMaxLength(20).HasDefaultValue("REGISTRADO");
        builder.Property(e => e.FechaHora).HasDefaultValueSql("NOW()");
        builder.Property(e => e.Notas).HasMaxLength(500);

        builder.HasOne(e => e.Usuario)
               .WithMany()
               .HasForeignKey(e => e.UsuarioId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.CashCut)
               .WithMany(c => c.Gastos)
               .HasForeignKey(e => e.CashCutId)
               .OnDelete(DeleteBehavior.SetNull);

        // El índice que sostiene la regla de aislamiento: toda consulta de un
        // empleado filtra por (usuario, día). Sin él, cada listado del móvil
        // termina en seq scan sobre la tabla general de gastos.
        builder.HasIndex(e => new { e.UsuarioId, e.FechaHora });
        builder.HasIndex(e => e.CashCutId);
    }
}
