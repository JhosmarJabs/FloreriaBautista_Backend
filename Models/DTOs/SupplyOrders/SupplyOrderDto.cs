using System.ComponentModel.DataAnnotations;

namespace FloreriaBautista.Models.DTOs.SupplyOrders;

// ── Listado ───────────────────────────────────────────────────────
public class SupplyOrderListItemDto
{
    public Guid      Id                 { get; set; }
    public string    Folio              { get; set; } = string.Empty;
    public string    Estado             { get; set; } = string.Empty;
    public string?   Proveedor          { get; set; }
    public DateTime  FechaSolicitud     { get; set; }
    public DateTime? FechaEnvio         { get; set; }
    public DateTime? FechaRecepcion     { get; set; }
    public string?   SemanaObjetivo     { get; set; }
    public int       TotalLineas        { get; set; }
    public int       LineasConfirmadas  { get; set; } // líneas con una cantidad recibida ya capturada
    public int       PorcentajeRecibido { get; set; } // confirmadas / total, 0–100
    public decimal   TotalEstimado      { get; set; }
}

// ── Detalle ───────────────────────────────────────────────────────
public class SupplyOrderDetailDto : SupplyOrderListItemDto
{
    public string?  Notas          { get; set; }
    public Guid     UsuarioId      { get; set; }
    public string?  UsuarioNombre  { get; set; }
    public List<SupplyOrderLineDto> Lineas { get; set; } = [];
}

public class SupplyOrderLineDto
{
    public Guid      Id                  { get; set; }
    public Guid      InventoryItemId     { get; set; }
    public string    NombreSnapshot      { get; set; } = string.Empty;
    public string?   UnidadMedida        { get; set; }
    public int       CantidadSolicitada  { get; set; }
    public int?      CantidadRecibida    { get; set; }
    public string    EstadoLinea         { get; set; } = string.Empty;
    public decimal?  PrecioUnitario      { get; set; }
    public string    Origen              { get; set; } = string.Empty;
    public string?   Observacion         { get; set; }
    public DateTime? RecibidoEn          { get; set; }
    public Guid?     InventoryMovementId { get; set; }
    public int       Diferencia          => (CantidadRecibida ?? 0) - CantidadSolicitada;
}

// ── Crear / editar ────────────────────────────────────────────────
public class CreateSupplyOrderDto
{
    public string? Proveedor      { get; set; }
    public string? SemanaObjetivo { get; set; }
    public string? Notas          { get; set; }

    [Required]
    [MinLength(1, ErrorMessage = "La solicitud debe llevar al menos un insumo.")]
    public List<SupplyOrderLineInputDto> Lineas { get; set; } = [];
}

public class UpdateSupplyOrderDto : CreateSupplyOrderDto { }

public class SupplyOrderLineInputDto
{
    [Required] public Guid InventoryItemId { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "La cantidad solicitada debe ser al menos 1.")]
    public int Cantidad { get; set; }

    public string? Origen { get; set; }
}

// ── Recepción ─────────────────────────────────────────────────────
public class ReceiveSupplyOrderDto
{
    [Required]
    [MinLength(1, ErrorMessage = "No se recibió ninguna línea para confirmar.")]
    public List<ReceiveSupplyOrderLineDto> Lineas { get; set; } = [];

    /// <summary>
    /// true cierra la solicitud con lo capturado; false la deja ENVIADA para
    /// permitir otra recepción en una visita posterior del proveedor.
    /// </summary>
    public bool CerrarSolicitud { get; set; } = true;
}

public class ReceiveSupplyOrderLineDto
{
    [Required] public Guid ItemId { get; set; } // Id de la línea (SupplyOrderItem), no del insumo

    [Range(0, int.MaxValue, ErrorMessage = "La cantidad recibida no puede ser negativa.")]
    public int CantidadRecibida { get; set; }

    public decimal? PrecioUnitario { get; set; }
    public string?  Observacion    { get; set; }
}

// ── Cancelación ───────────────────────────────────────────────────
public class CancelSupplyOrderDto
{
    public string? Motivo { get; set; }
}
