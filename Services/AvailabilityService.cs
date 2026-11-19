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

    public Task<AdvanceSaleValidationResult> ValidarVentaAnticipadaAsync(
        Guid productId, int cantidad, DateOnly fechaEntrega) =>
        ValidarVentaAnticipadaAsync([(productId, cantidad)], fechaEntrega);

    /// <summary>Valida un pedido anticipado completo: suma el consumo de todas las líneas por insumo
    /// y aplica sobre esa suma la lógica de ciclo de abastecimiento.</summary>
    public async Task<AdvanceSaleValidationResult> ValidarVentaAnticipadaAsync(
        IReadOnlyList<(Guid ProductId, int Cantidad)> lineas, DateOnly fechaEntrega)
    {
        var settings = await ObtenerSettingsAsync();

        var cantidades = lineas
            .GroupBy(l => l.ProductId)
            .ToDictionary(g => g.Key, g => g.Sum(l => l.Cantidad));
        var ids = cantidades.Keys.ToList();

        var productos = await _context.Products
            .Include(p => p.ProductRecipes)
                .ThenInclude(pr => pr.InventoryItem)
            .Where(p => ids.Contains(p.Id))
            .ToListAsync();

        foreach (var id in ids)
            if (productos.All(p => p.Id != id))
                throw new NotFoundException("Product", id);

        // Consumo total por insumo (en el orden en que aparece en las líneas).
        var consumo = new List<(Guid InsumoId, string Nombre, int Necesario)>();
        foreach (var id in ids)
        {
            var producto = productos.First(p => p.Id == id);
            foreach (var r in producto.ProductRecipes.Where(r => r.InventoryItem.Activo && r.CantidadRequerida > 0))
            {
                var necesario = r.CantidadRequerida * cantidades[id];
                var idx = consumo.FindIndex(c => c.InsumoId == r.InventoryItemId);
                if (idx >= 0)
                    consumo[idx] = (consumo[idx].InsumoId, consumo[idx].Nombre, consumo[idx].Necesario + necesario);
                else
                    consumo.Add((r.InventoryItemId, r.InventoryItem.Nombre, necesario));
            }
        }

        if (consumo.Count == 0 && settings.PermitirVentaSinReceta)
        {
            return new AdvanceSaleValidationResult
            {
                PuedeAceptarse = true,
                Mensaje = "Producto sin receta; venta permitida sin validación de stock."
            };
        }

        var faltantes = new List<(Guid InsumoId, string Nombre, int Faltante)>();
        foreach (var c in consumo)
        {
            var disp = await ObtenerDisponibilidadInsumoAsync(c.InsumoId);
            if (disp.Disponible < c.Necesario)
                faltantes.Add((c.InsumoId, c.Nombre, c.Necesario - disp.Disponible));
        }

        if (faltantes.Count == 0)
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

        foreach (var insumo in faltantes)
        {
            var faltante = insumo.Faltante;
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
                InventoryItemId = insumo.InsumoId,
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
            FechaLimiteCorte       = requiereAbastecimiento ? CorteAUtc(cutoff) : null,
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
            Cutoff                = CorteAUtc(cutoff),
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
        // Comprometido = consumo de pedidos activos que aún no descontaron del stock.
        // - Pedido normal: descuenta al entrar en EN_PREPARACION, así que solo cuenta en PENDIENTE_VALIDACION.
        // - Venta instantánea: descuenta al liquidar (SaldoPendiente == 0); mientras tenga saldo sigue contando.
        return await _context.OrderItems
            .Where(oi => oi.Order.EstadoPedido == "PENDIENTE_VALIDACION"
                      || oi.Order.EstadoPedido == "EN_PREPARACION"
                      || oi.Order.EstadoPedido == "EN_RUTA")
            .Where(oi =>
                (_context.SolicitudesVentaInstantanea.Any(s => s.OrderId == oi.OrderId) && oi.Order.SaldoPendiente > 0)
                || (!_context.SolicitudesVentaInstantanea.Any(s => s.OrderId == oi.OrderId)
                    && oi.Order.EstadoPedido == "PENDIENTE_VALIDACION"))
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

    /// <summary>Corte en hora local de la tienda (sin zona). Se compara con <c>AhoraLocal()</c>; para
    /// entregarlo en un DTO se convierte con <see cref="CorteAUtc"/>.</summary>
    internal static DateTime CutoffDateTime(DateOnly proximoAbastecimiento, int diaCutoff, TimeOnly horaCutoff)
    {
        var diffDias = ((int)proximoAbastecimiento.DayOfWeek - diaCutoff + 7) % 7;
        var cutoffDate = proximoAbastecimiento.AddDays(-diffDias);
        return cutoffDate.ToDateTime(horaCutoff, DateTimeKind.Unspecified);
    }

    /// <summary>Instante UTC real de un corte en hora local de la tienda. Usa <c>GetUtcOffset</c> (como
    /// <c>FechaHelper.InicioDelDiaUtc</c>) porque <c>ConvertTimeToUtc</c> lanza en horas inexistentes.</summary>
    internal static DateTime CorteAUtc(DateTime cutoffLocal, TimeZoneInfo zona)
    {
        var local = DateTime.SpecifyKind(cutoffLocal, DateTimeKind.Unspecified);
        return DateTime.SpecifyKind(local - zona.GetUtcOffset(local), DateTimeKind.Utc);
    }

    private DateTime CorteAUtc(DateTime cutoffLocal) => CorteAUtc(cutoffLocal, _fechas.Zona);
}
