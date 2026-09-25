using FloreriaBautista.Data;
using FloreriaBautista.Models.DTOs.Promotions;
using FloreriaBautista.Models.Entities;
using FloreriaBautista.Models.Exceptions;
using FloreriaBautista.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace FloreriaBautista.Services;

public class PricingService : IPricingService
{
    private readonly AppDbContext _context;

    public PricingService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<PricingBreakdownDto> CalcularTotalAsync(
        List<PricingItemDto> items, string? codigoCupon = null)
    {
        if (items.Count == 0)
            throw new AppException("El carrito está vacío.");

        var hoy = DateOnly.FromDateTime(DateTime.UtcNow);
        var descuentosAplicados = new List<DescuentoAplicadoDto>();

        // ── 1. Resolver productos y aplicar ofertas ───────────────
        var productIds = items.Select(i => i.ProductId).Distinct().ToList();
        var products = await _context.Products
            .Where(p => productIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id);

        // Ofertas activas vigentes para estos productos.
        var ofertas = await _context.Ofertas
            .Where(o => o.Activo
                && productIds.Contains(o.ProductoId)
                && (!o.FechaInicio.HasValue || o.FechaInicio <= hoy)
                && (!o.FechaFin.HasValue    || o.FechaFin   >= hoy))
            .ToDictionaryAsync(o => o.ProductoId);

        decimal subtotalSinOfertas = 0;
        decimal subtotalConOfertas = 0;

        // Datos por línea: precio unitario efectivo y categorías/catálogos.
        var lineas = new List<LineaCarrito>();

        foreach (var item in items)
        {
            if (!products.TryGetValue(item.ProductId, out var product))
                throw new AppException($"Producto '{item.ProductId}' no encontrado.");
            if (product.Estado != "ACTIVO")
                throw new AppException($"El producto '{product.Nombre}' no está disponible.");

            var precioBase = product.PrecioBase;
            var precioEfectivo = precioBase;
            subtotalSinOfertas += precioBase * item.Cantidad;

            if (ofertas.TryGetValue(item.ProductId, out var oferta))
            {
                precioEfectivo = oferta.PrecioOferta;
                var ahorro = (precioBase - precioEfectivo) * item.Cantidad;
                if (ahorro > 0)
                {
                    descuentosAplicados.Add(new DescuentoAplicadoDto
                    {
                        Origen = "OFERTA",
                        Nombre = $"Oferta en {product.Nombre}",
                        Monto  = ahorro
                    });
                }
            }

            subtotalConOfertas += precioEfectivo * item.Cantidad;
            lineas.Add(new LineaCarrito
            {
                ProductId      = product.Id,
                PrecioEfectivo = precioEfectivo,
                Cantidad       = item.Cantidad,
                SubtotalLinea  = precioEfectivo * item.Cantidad
            });
        }

        // ── 2. Descuentos automáticos ─────────────────────────────
        var descuentosActivos = await _context.Descuentos
            .Include(d => d.Categoria)
            .Include(d => d.Catalogo)
            .Where(d => d.Activo
                && (!d.FechaInicio.HasValue || d.FechaInicio <= hoy)
                && (!d.FechaFin.HasValue    || d.FechaFin   >= hoy))
            .ToListAsync();

        // Cargar categorías y catálogos de los productos del carrito.
        var productCategorias = await _context.ProductCategories
            .Where(pc => productIds.Contains(pc.ProductId))
            .ToListAsync();
        var productCatalogos = await _context.ProductCatalogos
            .Where(pc => productIds.Contains(pc.ProductId))
            .ToListAsync();

        DescuentoAplicadoDto? mejorDescuentoAuto = null;

        foreach (var desc in descuentosActivos)
        {
            decimal montoDescuento = 0;
            decimal baseParaCalculo;

            switch (desc.TipoRegla)
            {
                case "MONTO_MINIMO":
                    if (desc.MontoMinimoCompra.HasValue && subtotalConOfertas < desc.MontoMinimoCompra.Value)
                        continue;
                    baseParaCalculo = subtotalConOfertas;
                    montoDescuento = CalcularDescuento(desc.TipoValor, desc.Valor, baseParaCalculo);
                    break;

                case "CATEGORIA":
                    if (!desc.CategoriaId.HasValue) continue;
                    var prodsEnCat = productCategorias
                        .Where(pc => pc.CategoryId == desc.CategoriaId.Value)
                        .Select(pc => pc.ProductId)
                        .ToHashSet();
                    baseParaCalculo = lineas
                        .Where(l => prodsEnCat.Contains(l.ProductId))
                        .Sum(l => l.SubtotalLinea);
                    if (baseParaCalculo <= 0) continue;
                    montoDescuento = CalcularDescuento(desc.TipoValor, desc.Valor, baseParaCalculo);
                    break;

                case "CATALOGO":
                    if (!desc.CatalogoId.HasValue) continue;
                    var prodsEnCatalogo = productCatalogos
                        .Where(pc => pc.CatalogoId == desc.CatalogoId.Value)
                        .Select(pc => pc.ProductId)
                        .ToHashSet();
                    baseParaCalculo = lineas
                        .Where(l => prodsEnCatalogo.Contains(l.ProductId))
                        .Sum(l => l.SubtotalLinea);
                    if (baseParaCalculo <= 0) continue;
                    montoDescuento = CalcularDescuento(desc.TipoValor, desc.Valor, baseParaCalculo);
                    break;

                default:
                    continue;
            }

            if (montoDescuento > 0 && (mejorDescuentoAuto == null || montoDescuento > mejorDescuentoAuto.Monto))
            {
                mejorDescuentoAuto = new DescuentoAplicadoDto
                {
                    Origen = "DESCUENTO_AUTO",
                    Nombre = desc.Nombre,
                    Monto  = montoDescuento
                };
            }
        }

        // ── 3. Cupón (si se proporcionó) ──────────────────────────
        DescuentoAplicadoDto? descuentoCupon = null;
        if (!string.IsNullOrWhiteSpace(codigoCupon))
        {
            var cupon = await _context.Promotions
                .FirstOrDefaultAsync(p => p.Codigo == codigoCupon.Trim().ToUpper());

            if (cupon == null)
                throw new AppException("El código de cupón no existe.");
            if (cupon.Estado != "ACTIVO")
                throw new AppException("Este cupón no está activo.");
            if (cupon.FechaInicio.HasValue && cupon.FechaInicio > hoy)
                throw new AppException("Este cupón aún no es válido.");
            if (cupon.FechaFin.HasValue && cupon.FechaFin < hoy)
                throw new AppException("Este cupón ha expirado.");
            if (cupon.MaxUsos.HasValue && cupon.UsosActuales >= cupon.MaxUsos.Value)
                throw new AppException("Este cupón ya alcanzó su número máximo de usos.");
            if (cupon.MinimoCompra > 0 && subtotalConOfertas < cupon.MinimoCompra)
                throw new AppException(
                    $"El mínimo de compra para este cupón es ${cupon.MinimoCompra:N2}. " +
                    $"Tu subtotal actual es ${subtotalConOfertas:N2} (faltan ${(cupon.MinimoCompra - subtotalConOfertas):N2}).");

            var montoCupon = CalcularDescuento(cupon.Tipo, cupon.Valor, subtotalConOfertas);
            if (montoCupon > 0)
            {
                descuentoCupon = new DescuentoAplicadoDto
                {
                    Origen = "CUPON",
                    Nombre = $"Cupón {cupon.Codigo}",
                    Monto  = montoCupon
                };
            }
        }

        // ── 4. Elegir el mejor entre descuento automático y cupón (no se acumulan) ──
        DescuentoAplicadoDto? mejorDescuentoGeneral = null;
        if (mejorDescuentoAuto != null && descuentoCupon != null)
            mejorDescuentoGeneral = mejorDescuentoAuto.Monto >= descuentoCupon.Monto
                ? mejorDescuentoAuto : descuentoCupon;
        else
            mejorDescuentoGeneral = mejorDescuentoAuto ?? descuentoCupon;

        if (mejorDescuentoGeneral != null)
            descuentosAplicados.Add(mejorDescuentoGeneral);

        var totalDescuentos = descuentosAplicados.Sum(d => d.Monto);
        var total = Math.Max(0, subtotalConOfertas - (mejorDescuentoGeneral?.Monto ?? 0));

        // Nota: las ofertas ya están restadas del subtotalConOfertas (precio efectivo),
        // así que el subtotal que mostramos es el ORIGINAL sin ofertas para que se vea
        // el tachado, y el total refleja todo.
        return new PricingBreakdownDto
        {
            Subtotal            = subtotalSinOfertas,
            DescuentosAplicados = descuentosAplicados,
            TotalDescuentos     = totalDescuentos,
            Total               = total
        };
    }

    private static decimal CalcularDescuento(string tipoValor, decimal valor, decimal baseCalculo)
    {
        return tipoValor.ToUpper() switch
        {
            "PORCENTAJE" => Math.Round(baseCalculo * valor / 100, 2),
            "MONTO_FIJO" => Math.Min(valor, baseCalculo),
            _            => 0
        };
    }

    private class LineaCarrito
    {
        public Guid    ProductId      { get; init; }
        public decimal PrecioEfectivo { get; init; }
        public int     Cantidad       { get; init; }
        public decimal SubtotalLinea  { get; init; }
    }
}
