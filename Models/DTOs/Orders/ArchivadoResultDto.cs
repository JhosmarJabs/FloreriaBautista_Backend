namespace FloreriaBautista.Models.DTOs.Orders;

/// <summary>Resumen de una pasada del archivador de pedidos atrasados.</summary>
public class ArchivadoResultDto
{
    /// <summary>"Hoy" según la zona horaria de la tienda; se archivó todo lo anterior a esta fecha.</summary>
    public DateOnly Fecha { get; set; }

    /// <summary>Pedidos movidos al archivo en esta pasada.</summary>
    public int Total { get; set; }

    /// <summary>De los anteriores, cuántos se reescribieron a NO_COMPLETADO.</summary>
    public int NoCompletados { get; set; }

    /// <summary>De los anteriores, cuántos conservaron su estado en curso (EN_RUTA) y requieren cierre manual.</summary>
    public int RequierenCierre { get; set; }

    /// <summary>De los anteriores, cuántos ya venían entregados o cancelados.</summary>
    public int YaCerrados { get; set; }
}
