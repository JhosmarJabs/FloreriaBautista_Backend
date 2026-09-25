using FloreriaBautista.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FloreriaBautista.Data.Configurations;

public class CashCutConfiguration : IEntityTypeConfiguration<CashCut>
{
    public void Configure(EntityTypeBuilder<CashCut> builder)
    {
        builder.ToTable("cash_cuts");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).HasDefaultValueSql("gen_random_uuid()");
        builder.Property(c => c.FechaHoraCierre).HasDefaultValueSql("NOW()");
        builder.Property(c => c.Estado).HasMaxLength(20).HasDefaultValue("CERRADO");
        builder.Property(c => c.Notas).HasMaxLength(500);

        foreach (var monto in new[] { nameof(CashCut.TotalVentas), nameof(CashCut.TotalEfectivo),
                                      nameof(CashCut.TotalGastos), nameof(CashCut.EfectivoEsperado),
                                      nameof(CashCut.EfectivoDeclarado), nameof(CashCut.Diferencia) })
            builder.Property(monto).HasColumnType("numeric(10,2)").HasDefaultValue(0m);

        builder.HasOne(c => c.Usuario)
               .WithMany()
               .HasForeignKey(c => c.UsuarioId)
               .OnDelete(DeleteBehavior.Restrict);

        // Un solo corte cerrado por empleado y día: es lo que impide que alguien
        // cierre dos veces y duplique la recaudación del turno. Los anulados se
        // excluyen para que un corte cancelado por el admin no bloquee el rehacer.
        builder.HasIndex(c => new { c.UsuarioId, c.FechaLocal })
               .IsUnique()
               .HasFilter("estado <> 'ANULADO'");
    }
}
