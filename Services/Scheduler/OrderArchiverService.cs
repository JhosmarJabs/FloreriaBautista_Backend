using FloreriaBautista.Data;
using Microsoft.EntityFrameworkCore;

namespace FloreriaBautista.Services.Scheduler;

/// <summary>
/// Revisa periódicamente los pedidos cuya fecha de entrega ya pasó y no fueron
/// archivados. Si su estado ya es uno "final" (entregado o cancelado), solo se
/// mueve al archivo. Si nadie le dio seguimiento, se marca como "NO_COMPLETADO"
/// antes de archivarlo, para que no ensucie visualmente los pedidos del día.
/// </summary>
public class OrderArchiverService : BackgroundService
{
    private static readonly string[] EstadosFinales = ["ENTREGADO", "ENTREGADA", "CANCELADO", "CANCELADA"];
    private static readonly TimeSpan IntervaloRevision = TimeSpan.FromHours(1);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<OrderArchiverService> _logger;

    public OrderArchiverService(IServiceScopeFactory scopeFactory, ILogger<OrderArchiverService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger       = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("OrderArchiverService iniciado. Revisión cada {Horas}h.", IntervaloRevision.TotalHours);

        while (!stoppingToken.IsCancellationRequested)
        {
            await ArchivarPedidosAtrasadosAsync();
            await Task.Delay(IntervaloRevision, stoppingToken);
        }

        _logger.LogInformation("OrderArchiverService detenido.");
    }

    private async Task ArchivarPedidosAtrasadosAsync()
    {
        using var scope   = _scopeFactory.CreateScope();
        var       context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        try
        {
            var hoy = DateOnly.FromDateTime(DateTime.UtcNow);

            var atrasados = await context.Orders
                .Where(o => !o.Archivado && o.FechaEntrega < hoy)
                .ToListAsync();

            if (atrasados.Count == 0) return;

            var noCompletados = 0;
            foreach (var pedido in atrasados)
            {
                if (!EstadosFinales.Contains(pedido.EstadoPedido.ToUpper()))
                {
                    pedido.EstadoPedido = "NO_COMPLETADO";
                    noCompletados++;
                }

                pedido.Archivado   = true;
                pedido.ArchivadoEn = DateTime.UtcNow;
            }

            await context.SaveChangesAsync();

            _logger.LogInformation(
                "Archivado automático: {Total} pedido(s) atrasado(s) movidos al archivo ({NoCompletados} marcados como NO_COMPLETADO).",
                atrasados.Count, noCompletados);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al archivar pedidos atrasados");
        }
    }
}
