namespace FloreriaBautista.Models.Entities;

// Resultado de la segmentación RFM + clustering para un cliente. Se recalcula
// periódicamente y sobrescribe el registro anterior del mismo CustomerId.
public class CustomerSegment
{
    public Guid Id             { get; set; }
    public Guid CustomerId     { get; set; }
    public string Grupo        { get; set; } = string.Empty; // VIP / FRECUENTE / OCASIONAL / INACTIVO
    public int RecenciaDias    { get; set; }
    public int FrecuenciaPedidos { get; set; }
    public decimal MontoTotal  { get; set; }

    // Columnas demográficas (importancia 2 en el dataset de la Propuesta 3, ver
    // modelos predictivos/propuestas.md) — enriquecen la señal de entrada del k-means,
    // además de mostrarse en el detalle de cada cliente para apoyar campañas dirigidas.
    public int?    Edad                      { get; set; }
    public string? Sexo                      { get; set; }
    public string? Colonia                   { get; set; }
    public string? CategoriaFavorita         { get; set; }
    public string? CanalPreferido            { get; set; }
    public double  TasaCancelacion           { get; set; } // 0-1
    public int     AntiguedadDias            { get; set; } // desde Customer.CreadoEn
    public double? DiasPromedioEntrePedidos  { get; set; } // null si solo tiene 1 pedido

    public DateTime FechaCalculo { get; set; } = DateTime.UtcNow;

    public Customer Customer { get; set; } = null!;
}
