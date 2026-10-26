using FloreriaBautista.Data;
using FloreriaBautista.Models.DTOs.Inventory;
using FloreriaBautista.Models.Entities;
using FloreriaBautista.Models.Exceptions;
using FloreriaBautista.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace FloreriaBautista.Services;

public class AvailabilityService : IAvailabilityService
{
    private readonly AppDbContext  _context;
    private readonly IFechaHelper  _fechas;
    private readonly IMemoryCache  _cache;
    private readonly ILogger<AvailabilityService> _logger;

    private const string CacheSupplySettings = "supply-settings:v1";
    private static readonly TimeSpan TtlSettings = TimeSpan.FromMinutes(5);

    public AvailabilityService(
        AppDbContext context, IFechaHelper fechas,
        IMemoryCache cache, ILogger<AvailabilityService> logger)
    {
        _context = context;
        _fechas  = fechas;
        _cache   = cache;
        _logger  = logger;
    }

    // ── Disponibilidad de un insumo ──────────────────────────────────

    public async Task<SupplyAvailabilityDto> ObtenerDisponibilidadInsumoAsync(Guid inventoryItemId)
    {
        var item = await _context.InventoryItems.FindAsync(inventoryItemId)
            ?? throw new NotFoundException("InventoryItem", inventoryItemId);

        var comprometidoPendientes = await CalcularComprometidoPendientesAsync(inventoryItemId);
        var comprometidoReservas   = await CalcularComprometidoReservasAsync(inventoryItemId);
        var disponible = Math.Max(0, item.StockActual - comprometidoPendientes - comprometidoReservas);

        return new SupplyAvailabilityDto
        {
            InventoryItemId        = item.Id,
            Nombre                 = item.Nombre,
            StockActual            = item.StockActual,
            ComprometidoPendientes = comprometidoPendientes,
            ComprometidoReservas   = comprometidoReservas,
            Disponible             = disponible
        };
    }

    // ── Disponibilidad de un producto (cuello de botella) ────────────

    public async Task<ProductAvailabilityDto> ObtenerDisponibilidadProductoAsync(Guid productId)
    {
        var product = await _context.Products
            .Include(p => p.ProductRecipes)
                .ThenInclude(pr => pr.InventoryItem)
            .FirstOrDefaultAsync(p => p.Id == productId)
            ?? throw new NotFoundException("Product", productId);

        var recetas = product.ProductRecipes
            .Where(r => r.InventoryItem.Activo)
            .ToList();

        if (recetas.Count == 0)
        {
            return new ProductAvailabilityDto
            {
                ProductId          = product.Id,
                Nombre             = product.Nombre,
                CantidadDisponible = int.MaxValue,
                TieneReceta        = false
            };
        }

        var detalles = new List<InsumoDisponibilidadDto>();
        var minProductos = int.MaxValue;
        Guid? cuelloBotella = null;
        string? nombreCuello = null;

        foreach (var r in recetas)
        {
            var disp = await ObtenerDisponibilidadInsumoAsync(r.InventoryItemId);
            var alcanzaPara = r.CantidadRequerida > 0
                ? disp.Disponible / r.CantidadRequerida
                : int.MaxValue;

            detalles.Add(new InsumoDisponibilidadDto
            {
                InventoryItemId   = r.InventoryItemId,
                Nombre            = r.InventoryItem.Nombre,
                CantidadRequerida = r.CantidadRequerida,
                StockActual       = disp.StockActual,
                Comprometido      = disp.ComprometidoPendientes + disp.ComprometidoReservas,
                Disponible        = disp.Disponible,
                AlcanzaPara       = alcanzaPara
            });

            if (alcanzaPara < minProductos)
            {
                minProductos = alcanzaPara;
                cuelloBotella = r.InventoryItemId;
                nombreCuello = r.InventoryItem.Nombre;
            }
        }

        return new ProductAvailabilityDto
        {
            ProductId              = product.Id,
            Nombre                 = product.Nombre,
            CantidadDisponible     = Math.Max(0, minProductos),
            InsumoCuelloDeBotella  = cuelloBotella,
            NombreCuelloDeBotella  = nombreCuello,
            TieneReceta            = true,
            DetallesPorInsumo      = detalles
        };
    }

    // ── Validar venta instantánea ────────────────────────────────────

    public async Task<SaleValidationResult> ValidarVentaInstantaneaAsync(Guid productId, int cantidad)
    {
        var settings = await ObtenerSettingsAsync();
        var disp = await ObtenerDisponibilidadProductoAsync(productId);

        if (!disp.TieneReceta && settings.PermitirVentaSinReceta)
        {
            return new SaleValidationResult
            {
                PuedeVenderse = true,
                CantidadMaximaDisponible = int.MaxValue,
                DentroDeTolerancia = false,
                Deficit = 0
            };
        }

        if (!disp.TieneReceta)
        {
            return new SaleValidationResult
            {
                PuedeVenderse = false,
                CantidadMaximaDisponible = 0,
                Deficit = cantidad,
                InsumosInsuficientes = [new InsumoInsuficienteDto
                {
                    Nombre = "(sin receta)", Necesario = cantidad, Disponible = 0, Deficit = cantidad
                }]
            };
        }

        var deficit = Math.Max(0, cantidad - disp.CantidadDisponible);

        if (deficit == 0)
        {
            return new SaleValidationResult
            {
                PuedeVenderse = true,
                CantidadMaximaDisponible = disp.CantidadDisponible,
                Deficit = 0
            };
        }

        var dentroTolerancia = settings.UsarToleranciaAbsoluta
            ? deficit <= settings.ToleranciaUnidades
            : deficit <= (int)(cantidad * settings.ToleranciaPorcentaje);

        var insuficientes = disp.DetallesPorInsumo
            .Where(d => d.AlcanzaPara < cantidad)
            .Select(d => new InsumoInsuficienteDto
            {
                InventoryItemId = d.InventoryItemId,
                Nombre          = d.Nombre,
                Necesario       = d.CantidadRequerida * cantidad,
                Disponible      = d.Disponible,
                Deficit         = Math.Max(0, d.CantidadRequerida * cantidad - d.Disponible)
            })
            .ToList();

        return new SaleValidationResult
        {
            PuedeVenderse            = dentroTolerancia,
            CantidadMaximaDisponible = disp.CantidadDisponible,
            DentroDeTolerancia       = dentroTolerancia,
            Deficit                  = deficit,
            InsumosInsuficientes     = insuficientes
        };
    }

    // ── Validar venta anticipada ─────────────────────────────────────

    public async Task<AdvanceSaleValidationResult> ValidarVentaAnticipadaAsync(
        Guid productId, int cantidad, DateOnly fechaEntrega)
    {
        var settings = await ObtenerSettingsAsync();
        var disp = await ObtenerDisponibilidadProductoAsync(productId);

        if (!disp.TieneReceta && settings.PermitirVentaSinReceta)
        {
            return new AdvanceSaleValidationResult
            {
                PuedeAceptarse = true,
                Mensaje = "Producto sin receta; venta permitida sin validación de stock."
            };
        }

        if (cantidad <= disp.CantidadDisponible)
        {
            return new AdvanceSaleValidationResult
            {
                PuedeAceptarse = true,
                Mensaje = "Disponibilidad suficiente con stock actual."
            };
        }

        var hoy = _fechas.HoyLocal();
        var ahora = _fechas.AhoraLocal();
        var proximoAbastecimiento = ProximoDiaAbastecimiento(hoy, settings.DiaAbastecimiento);
        var cutoff = CutoffDateTime(proximoAbastecimiento, settings.DiaCutoff, settings.HoraCutoff);

        var insumosAbastecer = new List<InsumoAbastecimientoDto>();
        var todosViables = true;

        foreach (var insumo in disp.DetallesPorInsumo)
        {
            var necesario = insumo.CantidadRequerida * cantidad;
            if (insumo.Disponible >= necesario) continue;

            var faltante = necesario - insumo.Disponible;
            string estado;
            DateOnly? fechaViable = null;

            if (fechaEntrega >= proximoAbastecimiento)
            {
                if (ahora < cutoff)
                {
                    estado = "REQUIERE_PEDIDO";
                    fechaViable = proximoAbastecimiento;
                }
                else
                {
                    var siguienteCiclo = proximoAbastecimiento.AddDays(7);
                    if (fechaEntrega >= siguienteCiclo)
                    {
                        estado = "REQUIERE_PEDIDO";
                        fechaViable = siguienteCiclo;
                    }
                    else
                    {
                        estado = "SIN_TIEMPO";
                        fechaViable = siguienteCiclo;
                        todosViables = false;
                    }
                }
            }
            else
            {
                estado = "SIN_TIEMPO";
                fechaViable = proximoAbastecimiento;
                todosViables = false;
            }

            insumosAbastecer.Add(new InsumoAbastecimientoDto
            {
                InventoryItemId = insumo.InventoryItemId,
                Nombre          = insumo.Nombre,
                CantidadFaltante = faltante,
                FechaViable     = fechaViable,
                Estado          = estado
            });
        }

        var requiereAbastecimiento = insumosAbastecer.Count > 0;
        string mensaje;

        if (todosViables && requiereAbastecimiento)
        {
            var antesDelCorte = ahora < cutoff;
            mensaje = antesDelCorte
                ? "Puede agregarse al próximo pedido del proveedor."
                : $"El corte del pedido al proveedor ya cerró; la fecha más cercana es {proximoAbastecimiento.AddDays(7):yyyy-MM-dd}.";
        }
        else if (!todosViables)
        {
            var fechaSugerida = insumosAbastecer
                .Where(i => i.FechaViable.HasValue)
                .Max(i => i.FechaViable);
            mensaje = $"No hay tiempo suficiente para abastecer todos los insumos. Fecha sugerida: {fechaSugerida?.ToString("yyyy-MM-dd") ?? "N/A"}.";
        }
        else
        {
            mensaje = "Disponibilidad suficiente.";
        }

        return new AdvanceSaleValidationResult
        {
            PuedeAceptarse         = todosViables,
            RequiereAbastecimiento = requiereAbastecimiento,
            FechaLimiteCorte       = requiereAbastecimiento ? cutoff : null,
            Mensaje                = mensaje,
            FechaViableSugerida    = !todosViables
                ? insumosAbastecer.Where(i => i.FechaViable.HasValue).Max(i => i.FechaViable)
                : null,
            InsumosPorAbastecer    = insumosAbastecer
        };
    }

    // ── Ciclo de abastecimiento ──────────────────────────────────────

    public async Task<SupplyCycleDto> ObtenerCicloAbastecimientoAsync()
    {
        var settings = await ObtenerSettingsAsync();
        var hoy = _fechas.HoyLocal();
        var ahora = _fechas.AhoraLocal();
        var proximo = ProximoDiaAbastecimiento(hoy, settings.DiaAbastecimiento);
        var cutoff = CutoffDateTime(proximo, settings.DiaCutoff, settings.HoraCutoff);

        return new SupplyCycleDto
        {
            ProximoAbastecimiento = proximo,
            Cutoff                = cutoff,
            EstadoActual          = ahora < cutoff ? "ABIERTO" : "CERRADO"
        };
    }

    // ── Registrar demanda no cubierta ────────────────────────────────

    public async Task RegistrarDemandaNoCubiertaAsync(
        Guid inventoryItemId, Guid? productId, int solicitada, int disponible, string origen)
    {
        _context.UncoveredDemands.Add(new UncoveredDemand
        {
            Id                 = Guid.NewGuid(),
            InventoryItemId    = inventoryItemId,
            ProductId          = productId,
            CantidadSolicitada = solicitada,
            CantidadDisponible = disponible,
            Deficit            = Math.Max(0, solicitada - disponible),
            Origen             = origen,
            FechaRegistro      = _fechas.AhoraUtc()
        });
        await _context.SaveChangesAsync();
    }

    // ── Helpers privados ─────────────────────────────────────────────

    internal async Task<SupplySettings> ObtenerSettingsAsync()
    {
        if (_cache.TryGetValue(CacheSupplySettings, out SupplySettings? cached) && cached is not null)
            return cached;

        var settings = await _context.SupplySettings.FindAsync(1)
            ?? new SupplySettings();

        _cache.Set(CacheSupplySettings, settings, TtlSettings);
        return settings;
    }

    private async Task<int> CalcularComprometidoPendientesAsync(Guid inventoryItemId)
    {
        return await _context.OrderItems
            .Where(oi => oi.Order.EstadoPedido == "PENDIENTE_VALIDACION"
                      || oi.Order.EstadoPedido == "EN_PREPARACION"
                      || oi.Order.EstadoPedido == "EN_RUTA")
            .Where(oi => oi.Order.EstadoPedido != "CANCELADO")
            // Venta instantánea ya pagada: su stock ya se descontó al liquidar.
            .Where(oi => oi.Order.SaldoPendiente > 0
                      || !_context.SolicitudesVentaInstantanea.Any(s => s.OrderId == oi.OrderId))
            .Join(
                _context.ProductRecipes.Where(pr => pr.InventoryItemId == inventoryItemId),
                oi => oi.ProductId,
                pr => pr.ProductId,
                (oi, pr) => oi.Cantidad * pr.CantidadRequerida)
            .SumAsync(x => (int?)x) ?? 0;
    }

    private async Task<int> CalcularComprometidoReservasAsync(Guid inventoryItemId)
    {
        var ahora = _fechas.AhoraUtc();

        return await _context.SolicitudesVentaInstantanea
            .Where(s => s.Estado == "ACEPTADA"
                     && s.ReservaExpiraEn != null
                     && s.ReservaExpiraEn > ahora
                     && s.OrderId == null)
            .Join(
                _context.ProductRecipes.Where(pr => pr.InventoryItemId == inventoryItemId),
                s => s.ProductId,
                pr => pr.ProductId,
                (s, pr) => s.Cantidad * pr.CantidadRequerida)
            .SumAsync(x => (int?)x) ?? 0;
    }

    internal static DateOnly ProximoDiaAbastecimiento(DateOnly hoy, int diaAbastecimiento)
    {
        var diasHasta = ((diaAbastecimiento - (int)hoy.DayOfWeek) + 7) % 7;
        if (diasHasta == 0) diasHasta = 7;
        return hoy.AddDays(diasHasta);
    }

    internal static DateTime CutoffDateTime(DateOnly proximoAbastecimiento, int diaCutoff, TimeOnly horaCutoff)
    {
        var diffDias = ((int)proximoAbastecimiento.DayOfWeek - diaCutoff + 7) % 7;
        var cutoffDate = proximoAbastecimiento.AddDays(-diffDias);
        return cutoffDate.ToDateTime(horaCutoff, DateTimeKind.Utc);
    }
}
