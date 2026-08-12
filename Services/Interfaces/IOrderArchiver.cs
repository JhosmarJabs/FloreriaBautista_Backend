using FloreriaBautista.Models.DTOs.Orders;

namespace FloreriaBautista.Services.Interfaces;

public interface IOrderArchiver
{
    /// <summary>
    /// Archiva los pedidos cuya fecha de entrega ya pasó (según la hora local de
    /// la tienda). Es idempotente: un pedido ya archivado no se vuelve a tocar.
    /// </summary>
    /// <param name="usuarioId">Admin que forzó la pasada; null si la ejecutó el scheduler.</param>
    Task<ArchivadoResultDto> ArchivarAtrasadosAsync(Guid? usuarioId = null);
}
