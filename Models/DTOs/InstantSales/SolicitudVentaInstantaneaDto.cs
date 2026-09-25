namespace FloreriaBautista.Models.DTOs.InstantSales;

/// <summary>Resumen de una solicitud de venta instantanea para listados admin.</summary>
public class SolicitudVentaInstantaneaDto
{
    public Guid     Id                   { get; set; }
    public Guid     CustomerId           { get; set; }
    public string   NombreCliente        { get; set; } = string.Empty;
    public string?  TelefonoCliente      { get; set; }
    public Guid     ProductId            { get; set; }
    public string   NombreProducto       { get; set; } = string.Empty;
    public string?  ImagenProducto       { get; set; }
    public int      Cantidad             { get; set; }
    public string   Estado               { get; set; } = string.Empty;
    public DateTime CreadaEn             { get; set; }
    public DateTime? EscaladaAEmpleadoEn { get; set; }
    public DateTime? DecididaEn          { get; set; }
    public string?  DecididaPorNombre    { get; set; }
    public string?  MotivoRechazo        { get; set; }
    public string?  MotivoExpiracion     { get; set; }
    public DateTime? ReservaExpiraEn     { get; set; }
    public Guid?    OrderId              { get; set; }
}
