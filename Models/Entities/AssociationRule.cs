namespace FloreriaBautista.Models.Entities;

// Regla de asociación (market basket) minada de order_items: cuando el cliente
// compra ProductIdAntecedente, con Confianza% también compra ProductIdConsecuente.
public class AssociationRule
{
    public Guid Id                      { get; set; }
    public Guid ProductIdAntecedente    { get; set; }
    public Guid ProductIdConsecuente    { get; set; }
    public decimal Soporte              { get; set; } // % de transacciones que contienen ambos productos
    public decimal Confianza            { get; set; } // % de transacciones con el antecedente que también tienen el consecuente
    public DateTime CalculadoEn         { get; set; } = DateTime.UtcNow;

    public Product ProductoAntecedente { get; set; } = null!;
    public Product ProductoConsecuente { get; set; } = null!;
}
